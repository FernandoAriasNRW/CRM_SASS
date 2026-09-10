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
public sealed class CalendarioFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    /// <summary>Crea un evento y devuelve su identificador.</summary>
    private static async Task<Guid> CrearEventoAsync(HttpClient cliente, string titulo, DateTime inicio, Guid? ticketId = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/calendar/events", new
        {
            Title = titulo,
            StartTime = inicio,
            EndTime = inicio.AddHours(1),
            Type = "Meeting",
            TicketId = ticketId,
            Description = "Creado por las pruebas",
            Location = "Sala"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement[]> EventosAsync(HttpClient cliente, string ruta = "/api/v1/calendar/events?pageSize=500")
    {
        var cuerpo = await cliente.GetFromJsonAsync<JsonElement>(ruta);
        return [.. cuerpo.GetProperty("items").EnumerateArray()];
    }

    /// <summary>
    /// El fallo con su nombre: anular no puede hacer desaparecer la reunión.
    ///
    /// Si desaparece, quien mira el jueves no ve nada y se presenta igual. Por eso se comprueba
    /// que <b>sigue en la lista</b> y además que está marcada, y no sólo que la llamada devuelve
    /// 200 —también lo devolvía cuando borraba—.
    /// </summary>
    [Fact]
    public async Task Un_evento_anulado_sigue_en_el_calendario_pero_marcado()
    {
        var cliente = await AutenticarAsync();
        var id = await CrearEventoAsync(cliente, "Reunión que se anula", new DateTime(2027, 3, 10, 10, 0, 0));

        var anular = await cliente.PostAsJsonAsync($"/api/v1/calendar/events/{id}/anular", new { Motivo = "El cliente lo aplaza" });
        anular.StatusCode.Should().Be(HttpStatusCode.OK);

        var enLista = (await EventosAsync(cliente)).SingleOrDefault(e => e.GetProperty("id").GetGuid() == id);

        enLista.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "un evento anulado sigue estando: si desaparece, la gente se presenta a una reunión que ya no existe");

        enLista.GetProperty("canceladoEnUtc").ValueKind.Should().NotBe(JsonValueKind.Null,
            "tiene que venir marcado para poder pintarlo tachado");
        enLista.GetProperty("motivoDeCancelacion").GetString().Should().Be("El cliente lo aplaza");
    }

    [Fact]
    public async Task Anular_se_puede_deshacer()
    {
        var cliente = await AutenticarAsync();
        var id = await CrearEventoAsync(cliente, "Reunión que se recupera", new DateTime(2027, 3, 11, 10, 0, 0));

        await cliente.PostAsJsonAsync($"/api/v1/calendar/events/{id}/anular", new { Motivo = (string?)null });

        var reactivar = await cliente.PostAsync($"/api/v1/calendar/events/{id}/reactivar", null);
        reactivar.StatusCode.Should().Be(HttpStatusCode.OK);

        var evento = (await EventosAsync(cliente)).Single(e => e.GetProperty("id").GetGuid() == id);
        evento.GetProperty("canceladoEnUtc").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>
    /// La papelera es distinta de anular: quita el evento de la vista, y tiene vuelta.
    ///
    /// Lo que se comprueba es el recorrido entero. Restaurar respondía «Evento no encontrado»
    /// teniendo la fila delante, así que lo que iba a la papelera no salía nunca — una prueba que
    /// sólo mirase el borrado habría pasado.
    /// </summary>
    [Fact]
    public async Task De_la_papelera_se_vuelve()
    {
        var cliente = await AutenticarAsync();
        var id = await CrearEventoAsync(cliente, "Reunión creada por error", new DateTime(2027, 3, 12, 10, 0, 0));

        var borrar = await cliente.DeleteAsync($"/api/v1/calendar/events/{id}");
        borrar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await EventosAsync(cliente)).Select(e => e.GetProperty("id").GetGuid())
            .Should().NotContain(id, "lo que está en la papelera no se pinta en el calendario");

        (await EventosAsync(cliente, "/api/v1/calendar/events/papelera"))
            .Select(e => e.GetProperty("id").GetGuid())
            .Should().Contain(id, "la papelera existía en el repositorio y no la exponía ningún endpoint");

        var restaurar = await cliente.PostAsync($"/api/v1/calendar/events/{id}/restaurar", null);
        restaurar.StatusCode.Should().Be(HttpStatusCode.OK,
            "restaurar contestaba «Evento no encontrado» porque el filtro global escondía la fila "
            + "que el propio parámetro `includeDeleted` decía traer");

        (await EventosAsync(cliente)).Select(e => e.GetProperty("id").GetGuid()).Should().Contain(id);
    }

    /// <summary>
    /// Los enlaces se mandan los tres a la vez, y un nulo quita el enlace.
    ///
    /// Es lo que hace posible desenlazar. Con campos opcionales, «quítalo» y «no lo toques» serían
    /// indistinguibles y el enlace se quedaría puesto para siempre.
    /// </summary>
    [Fact]
    public async Task El_evento_se_enlaza_con_un_ticket_y_se_desenlaza()
    {
        var cliente = await AutenticarAsync();

        var tickets = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tickets?pageSize=1");
        var ticketId = tickets.GetProperty("items")[0].GetProperty("id").GetGuid();

        var tareas = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tasks?pageSize=1");
        var tareaId = tareas.GetProperty("items")[0].GetProperty("id").GetGuid();

        // Enlazado desde el alta: el campo faltaba en la entidad, sólo había proyecto y tarea.
        var id = await CrearEventoAsync(cliente, "Seguimiento del ticket", new DateTime(2027, 3, 13, 10, 0, 0), ticketId);

        var recienCreado = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/calendar/events/{id}");
        recienCreado.GetProperty("ticketId").GetGuid().Should().Be(ticketId);

        var enlazar = await cliente.PutAsJsonAsync($"/api/v1/calendar/events/{id}/enlaces",
            new { ProjectId = (Guid?)null, TaskId = tareaId, TicketId = ticketId });
        enlazar.StatusCode.Should().Be(HttpStatusCode.OK);

        var conLosDos = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/calendar/events/{id}");
        conLosDos.GetProperty("taskId").GetGuid().Should().Be(tareaId,
            "un evento puede colgar de un ticket y de una tarea a la vez");
        conLosDos.GetProperty("ticketId").GetGuid().Should().Be(ticketId);

        var desenlazar = await cliente.PutAsJsonAsync($"/api/v1/calendar/events/{id}/enlaces",
            new { ProjectId = (Guid?)null, TaskId = tareaId, TicketId = (Guid?)null });
        desenlazar.StatusCode.Should().Be(HttpStatusCode.OK);

        var sinTicket = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/calendar/events/{id}");
        sinTicket.GetProperty("ticketId").ValueKind.Should().Be(JsonValueKind.Null,
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
    public async Task El_paginado_dice_la_verdad()
    {
        var cliente = await AutenticarAsync();

        var cuerpo = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/calendar/events?page=1&pageSize=2");

        cuerpo.GetProperty("page").GetInt32().Should().Be(1);
        cuerpo.GetProperty("pageSize").GetInt32().Should().Be(2);
        cuerpo.GetProperty("items").GetArrayLength().Should().BeLessThanOrEqualTo(2);

        var total = cuerpo.GetProperty("totalCount").GetInt32();
        var todos = await EventosAsync(cliente);

        total.Should().Be(todos.Length,
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
    public async Task La_agenda_del_dia_coincide_con_lo_que_dicen_los_modulos()
    {
        var cliente = await AutenticarAsync();

        var dia = new DateTime(2027, 4, 20);
        await CrearEventoAsync(cliente, "Reunión de la agenda", dia.AddHours(9));

        var agenda = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/calendar/agenda/{dia:yyyy-MM-dd}");

        agenda.GetProperty("eventos").EnumerateArray()
            .Select(e => e.GetProperty("titulo").GetString())
            .Should().Contain("Reunión de la agenda");

        // Y las otras tres secciones vienen, aunque estén vacías: una agenda a la que le falta un
        // apartado según el día hace pensar que la aplicación se comporta distinto cada vez.
        foreach (var seccion in new[] { "tareasQueVencen", "ticketsDelDia", "proyectosQueTerminan" })
        {
            agenda.TryGetProperty(seccion, out var lista).Should().BeTrue($"falta «{seccion}»");
            lista.ValueKind.Should().Be(JsonValueKind.Array);
        }

        var tickets = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tickets?pageSize=1");
        var unTicket = tickets.GetProperty("items")[0];
        var diaDelTicket = DateOnly.FromDateTime(unTicket.GetProperty("createdAt").GetDateTime());

        var agendaDelTicket = await cliente.GetFromJsonAsync<JsonElement>(
            $"/api/v1/calendar/agenda/{diaDelTicket:yyyy-MM-dd}");

        agendaDelTicket.GetProperty("ticketsDelDia").EnumerateArray()
            .Select(t => t.GetProperty("id").GetGuid())
            .Should().Contain(unTicket.GetProperty("id").GetGuid(),
                "un ticket abierto ese día tiene que salir en la agenda de ese día");
    }

    /// <summary>
    /// Anular y borrar no se pisan: lo que está en la papelera no se puede anular.
    ///
    /// Es la comprobación que separa los dos conceptos. Si dejara, quedaría un evento «anulado»
    /// que además no se ve, y nadie sabría cuál de las dos cosas le pasó.
    /// </summary>
    [Fact]
    public async Task Lo_que_esta_en_la_papelera_no_se_anula()
    {
        var cliente = await AutenticarAsync();
        var id = await CrearEventoAsync(cliente, "Reunión que se borra", new DateTime(2027, 3, 14, 10, 0, 0));

        await cliente.DeleteAsync($"/api/v1/calendar/events/{id}");

        var anular = await cliente.PostAsJsonAsync($"/api/v1/calendar/events/{id}/anular", new { Motivo = (string?)null });
        anular.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
