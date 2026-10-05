using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El calendario: anular sin desaparecer, la papelera con vuelta, los enlaces y la agenda del día.
///
/// <b>Casi todo esto respondía y no hacía lo que decía.</b> «Cancelar» era en realidad borrar, así
/// que anular una reunión la quitaba del calendario y quien la buscaba el jueves no encontraba
/// nada. La papelera tenía consulta en el repositorio y ningún endpoint. Y restaurar contestaba
/// «Evento no encontrado» teniendo la fila delante, porque el parámetro <c>includeDeleted</c> sólo
/// quitaba un <c>Where</c> a mano mientras el filtro global seguía escondiéndola.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CalendarFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    /// <summary>Crea un evento y devuelve su identificador.</summary>
    private static async Task<Guid> CreateEventAsync(HttpClient client, string title, DateTime start, Guid? ticketId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/calendar/events", new
        {
            Title = title,
            StartTime = start,
            EndTime = start.AddHours(1),
            Type = "Meeting",
            TicketId = ticketId,
            Description = "Creado por las pruebas",
            Location = "Sala"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement[]> EventsAsync(HttpClient client, string path = "/api/v1/calendar/events?pageSize=500")
    {
        var body = await client.GetFromJsonAsync<JsonElement>(path);
        return [.. body.GetProperty("items").EnumerateArray()];
    }

    /// <summary>
    /// El fallo con su nombre: anular no puede hacer desaparecer la reunión.
    ///
    /// Si desaparece, quien mira el jueves no ve nada y se presenta igual. Por eso se comprueba
    /// que <b>sigue en la lista</b> y además que está marcada, y no sólo que la llamada devuelve
    /// 200 —también lo devolvía cuando borraba—.
    /// </summary>
    [Fact]
    public async Task A_cancelled_event_stays_on_the_calendar_but_marked()
    {
        var client = await AuthenticateAsync();
        var id = await CreateEventAsync(client, "Reunión que se anula", new DateTime(2027, 3, 10, 10, 0, 0));

        var cancel = await client.PostAsJsonAsync($"/api/v1/calendar/events/{id}/cancel", new { Reason = "El cliente lo aplaza" });
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);

        var inList = (await EventsAsync(client)).SingleOrDefault(e => e.GetProperty("id").GetGuid() == id);

        inList.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "un evento anulado sigue estando: si desaparece, la gente se presenta a una reunión que ya no existe");

        inList.GetProperty("cancelledAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null,
            "tiene que venir marcado para poder pintarlo tachado");
        inList.GetProperty("cancellationReason").GetString().Should().Be("El cliente lo aplaza");
    }

    [Fact]
    public async Task Cancelling_can_be_undone()
    {
        var client = await AuthenticateAsync();
        var id = await CreateEventAsync(client, "Reunión que se recupera", new DateTime(2027, 3, 11, 10, 0, 0));

        await client.PostAsJsonAsync($"/api/v1/calendar/events/{id}/cancel", new { Reason = (string?)null });

        var reactivate = await client.PostAsync($"/api/v1/calendar/events/{id}/reactivate", null);
        reactivate.StatusCode.Should().Be(HttpStatusCode.OK);

        var calendarEvent = (await EventsAsync(client)).Single(e => e.GetProperty("id").GetGuid() == id);
        calendarEvent.GetProperty("cancelledAtUtc").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>
    /// La papelera es distinta de anular: quita el evento de la vista, y tiene vuelta.
    ///
    /// Lo que se comprueba es el recorrido entero. Restaurar respondía «Evento no encontrado»
    /// teniendo la fila delante, así que lo que iba a la papelera no salía nunca — una prueba que
    /// sólo mirase el borrado habría pasado.
    /// </summary>
    [Fact]
    public async Task It_comes_back_from_the_trash()
    {
        var client = await AuthenticateAsync();
        var id = await CreateEventAsync(client, "Reunión creada por error", new DateTime(2027, 3, 12, 10, 0, 0));

        var remove = await client.DeleteAsync($"/api/v1/calendar/events/{id}");
        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EventsAsync(client)).Select(e => e.GetProperty("id").GetGuid())
            .Should().NotContain(id, "lo que está en la papelera no se pinta en el calendario");

        (await EventsAsync(client, "/api/v1/calendar/events/trash"))
            .Select(e => e.GetProperty("id").GetGuid())
            .Should().Contain(id, "la papelera existía en el repositorio y no la exponía ningún endpoint");

        var restore = await client.PostAsync($"/api/v1/calendar/events/{id}/restore", null);
        restore.StatusCode.Should().Be(HttpStatusCode.OK,
            "restaurar contestaba «Evento no encontrado» porque el filtro global escondía la fila "
            + "que el propio parámetro `includeDeleted` decía traer");

        (await EventsAsync(client)).Select(e => e.GetProperty("id").GetGuid()).Should().Contain(id);
    }

    /// <summary>
    /// Los enlaces se mandan los tres a la vez, y un nulo quita el enlace.
    ///
    /// Es lo que hace posible desenlazar. Con campos opcionales, «quítalo» y «no lo toques» serían
    /// indistinguibles y el enlace se quedaría puesto para siempre.
    /// </summary>
    [Fact]
    public async Task An_event_links_to_a_ticket_and_unlinks()
    {
        var client = await AuthenticateAsync();

        var tickets = await client.GetFromJsonAsync<JsonElement>("/api/v1/tickets?pageSize=1");
        var ticketId = tickets.GetProperty("items")[0].GetProperty("id").GetGuid();

        var tasks = await client.GetFromJsonAsync<JsonElement>("/api/v1/tasks?pageSize=1");
        var taskId = tasks.GetProperty("items")[0].GetProperty("id").GetGuid();

        // Enlazado desde el alta: el campo faltaba en la entidad, sólo había proyecto y tarea.
        var id = await CreateEventAsync(client, "Seguimiento del ticket", new DateTime(2027, 3, 13, 10, 0, 0), ticketId);

        var justCreated = await client.GetFromJsonAsync<JsonElement>($"/api/v1/calendar/events/{id}");
        justCreated.GetProperty("ticketId").GetGuid().Should().Be(ticketId);

        var link = await client.PutAsJsonAsync($"/api/v1/calendar/events/{id}/links",
            new { ProjectId = (Guid?)null, TaskId = taskId, TicketId = ticketId });
        link.StatusCode.Should().Be(HttpStatusCode.OK);

        var withBoth = await client.GetFromJsonAsync<JsonElement>($"/api/v1/calendar/events/{id}");
        withBoth.GetProperty("taskId").GetGuid().Should().Be(taskId,
            "un evento puede colgar de un ticket y de una tarea a la vez");
        withBoth.GetProperty("ticketId").GetGuid().Should().Be(ticketId);

        var desenlazar = await client.PutAsJsonAsync($"/api/v1/calendar/events/{id}/links",
            new { ProjectId = (Guid?)null, TaskId = taskId, TicketId = (Guid?)null });
        desenlazar.StatusCode.Should().Be(HttpStatusCode.OK);

        var withoutTicket = await client.GetFromJsonAsync<JsonElement>($"/api/v1/calendar/events/{id}");
        withoutTicket.GetProperty("ticketId").ValueKind.Should().Be(JsonValueKind.Null,
            "mandar el ticket en nulo es cómo se quita el enlace");
    }

    /// <summary>
    /// El paginado del calendario decía cualquier cosa.
    ///
    /// <c>PagedResult.Create</c> recibe <c>(items, totalCount, page, pageSize)</c> y se le pasaba
    /// <c>(dtos, page, pageSize, totalCount)</c>: tres enteros seguidos compilan en cualquier
    /// orden. La respuesta traía bien los eventos y mentía en los totales, que es de lo que
    /// depende cualquier paginador de la pantalla.
    /// </summary>
    [Fact]
    public async Task Paging_tells_the_truth()
    {
        var client = await AuthenticateAsync();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/calendar/events?page=1&pageSize=2");

        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(2);
        body.GetProperty("items").GetArrayLength().Should().BeLessThanOrEqualTo(2);

        var total = body.GetProperty("totalCount").GetInt32();
        var all = await EventsAsync(client);

        total.Should().Be(all.Length,
            "el total tiene que ser el número de eventos, no la página ni el tamaño de página");
    }

    /// <summary>
    /// La agenda de un día junta cuatro módulos en una sola respuesta.
    ///
    /// Se comprueba contra las listas de cada módulo en vez de contra números escritos aquí: lo
    /// que importa es que la agenda diga lo mismo que dicen las pantallas de las que sale, que es
    /// exactamente lo que fallaba en las exportaciones —el fichero decía 0 y la API decía 15—.
    /// </summary>
    [Fact]
    public async Task The_daily_agenda_matches_what_the_modules_say()
    {
        var client = await AuthenticateAsync();

        var day = new DateTime(2027, 4, 20);
        await CreateEventAsync(client, "Reunión de la agenda", day.AddHours(9));

        var agenda = await client.GetFromJsonAsync<JsonElement>($"/api/v1/calendar/agenda/{day:yyyy-MM-dd}");

        agenda.GetProperty("events").EnumerateArray()
            .Select(e => e.GetProperty("title").GetString())
            .Should().Contain("Reunión de la agenda");

        // Y las otras tres secciones vienen, aunque estén vacías: una agenda a la que le falta un
        // apartado según el día hace pensar que la aplicación se comporta distinto cada vez.
        foreach (var section in new[] { "tasksDue", "ticketsOpened", "projectsEnding" })
        {
            agenda.TryGetProperty(section, out var list).Should().BeTrue($"falta «{section}»");
            list.ValueKind.Should().Be(JsonValueKind.Array);
        }

        var tickets = await client.GetFromJsonAsync<JsonElement>("/api/v1/tickets?pageSize=1");
        var aTicket = tickets.GetProperty("items")[0];
        var ticketDay = DateOnly.FromDateTime(aTicket.GetProperty("createdAt").GetDateTime());

        var ticketAgenda = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/calendar/agenda/{ticketDay:yyyy-MM-dd}");

        ticketAgenda.GetProperty("ticketsOpened").EnumerateArray()
            .Select(t => t.GetProperty("id").GetGuid())
            .Should().Contain(aTicket.GetProperty("id").GetGuid(),
                "un ticket abierto ese día tiene que salir en la agenda de ese día");
    }

    /// <summary>
    /// Anular y borrar no se pisan: lo que está en la papelera no se puede anular.
    ///
    /// Es la comprobación que separa los dos conceptos. Si dejara, quedaría un evento «anulado»
    /// que además no se ve, y nadie sabría cuál de las dos cosas le pasó.
    /// </summary>
    [Fact]
    public async Task What_is_in_the_trash_cannot_be_cancelled()
    {
        var client = await AuthenticateAsync();
        var id = await CreateEventAsync(client, "Reunión que se borra", new DateTime(2027, 3, 14, 10, 0, 0));

        await client.DeleteAsync($"/api/v1/calendar/events/{id}");

        var cancel = await client.PostAsJsonAsync($"/api/v1/calendar/events/{id}/cancel", new { Reason = (string?)null });
        cancel.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
