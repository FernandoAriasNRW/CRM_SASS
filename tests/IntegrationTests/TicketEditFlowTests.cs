using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Editar un ticket por <c>PATCH /tickets/{id}</c>.
///
/// <b>El endpoint estaba publicado y no había handler.</b> MediatR no tenía a quién entregarle
/// <c>UpdateTicketCommand</c>, así que cada intento acababa en un 500 —«No service for type
/// IRequestHandler»— y el tablero devolvía la tarjeta a su columna diciendo «no se pudo mover».
/// Guardar la ficha de un ticket tampoco guardaba nada.
///
/// Las pruebas comprueban el efecto y no el código de respuesta: que el ticket <b>quedó</b> en el
/// otro estado, y que un título inválido <b>no deja nada a medio guardar</b>.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TicketEditFlowTests(CrmApiFactory factory)
{
    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<Guid> CreateTicketAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tickets", new
        {
            Title = title,
            Description = "Creado por las pruebas de edición",
            Priority = "Medium"
        });
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/v1/tickets/{id}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Arrastrar una tarjeta manda sólo el estado, y el ticket se queda donde se soltó.</summary>
    [Fact]
    public async Task Changing_only_the_status_moves_the_ticket_and_keeps_the_rest()
    {
        var client = await AuthenticateAsync();
        var id = await CreateTicketAsync(client, "Mover esta tarjeta entre columnas");

        var response = await client.PatchAsJsonAsync($"/api/v1/tickets/{id}", new { Status = "InProgress" });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var ticket = await ReadAsync(client, id);
        ticket.GetProperty("status").GetString().Should().Be("InProgress");

        // Lo que no se mandó se queda como estaba. Una actualización total dejaría el título y la
        // descripción en blanco cada vez que alguien arrastra una tarjeta.
        ticket.GetProperty("title").GetString().Should().Be("Mover esta tarjeta entre columnas");
        ticket.GetProperty("description").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>Un título inválido se rechaza entero: ni el título ni el estado se quedan a medias.</summary>
    [Fact]
    public async Task An_invalid_title_saves_nothing()
    {
        var client = await AuthenticateAsync();
        var id = await CreateTicketAsync(client, "Este título sí es válido");

        var response = await client.PatchAsJsonAsync($"/api/v1/tickets/{id}",
            new { Title = "abc", Status = "Resolved" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "un título corto es un dato mal formado, no un ticket que no existe");

        var ticket = await ReadAsync(client, id);
        ticket.GetProperty("title").GetString().Should().Be("Este título sí es válido");
        ticket.GetProperty("status").GetString().Should().Be("Open",
            "el estado viajaba en la misma petición: si se hubiera aplicado antes de validar el "
            + "título, el ticket quedaría medio guardado");
    }

    /// <summary>Editar la ficha entera guarda los cuatro campos de una vez.</summary>
    [Fact]
    public async Task Editing_the_detail_saves_title_description_and_priority()
    {
        var client = await AuthenticateAsync();
        var id = await CreateTicketAsync(client, "Ficha por editar desde la pantalla");

        var response = await client.PatchAsJsonAsync($"/api/v1/tickets/{id}", new
        {
            Title = "Ficha ya editada desde la pantalla",
            Description = "Descripción nueva",
            Priority = "High",
            Status = "PendingInfo"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var ticket = await ReadAsync(client, id);
        ticket.GetProperty("title").GetString().Should().Be("Ficha ya editada desde la pantalla");
        ticket.GetProperty("description").GetString().Should().Be("Descripción nueva");
        ticket.GetProperty("priority").GetString().Should().Be("High");
        ticket.GetProperty("status").GetString().Should().Be("PendingInfo");
    }
}
