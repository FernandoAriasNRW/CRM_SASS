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
public sealed class EdicionDeTicketFlowTests(CrmApiFactory factory)
{
    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static async Task<Guid> CrearTicketAsync(HttpClient cliente, string titulo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tickets", new
        {
            Title = titulo,
            Description = "Creado por las pruebas de edición",
            Priority = "Medium"
        });
        respuesta.EnsureSuccessStatusCode();

        var creado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return creado.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> LeerAsync(HttpClient cliente, Guid id)
    {
        var respuesta = await cliente.GetAsync($"/api/v1/tickets/{id}");
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Arrastrar una tarjeta manda sólo el estado, y el ticket se queda donde se soltó.</summary>
    [Fact]
    public async Task Cambiar_solo_el_estado_mueve_el_ticket_y_no_toca_lo_demas()
    {
        var cliente = await AutenticarAsync();
        var id = await CrearTicketAsync(cliente, "Mover esta tarjeta entre columnas");

        var respuesta = await cliente.PatchAsJsonAsync($"/api/v1/tickets/{id}", new { Status = "InProgress" });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var ticket = await LeerAsync(cliente, id);
        ticket.GetProperty("status").GetString().Should().Be("InProgress");

        // Lo que no se mandó se queda como estaba. Una actualización total dejaría el título y la
        // descripción en blanco cada vez que alguien arrastra una tarjeta.
        ticket.GetProperty("title").GetString().Should().Be("Mover esta tarjeta entre columnas");
        ticket.GetProperty("description").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>Un título inválido se rechaza entero: ni el título ni el estado se quedan a medias.</summary>
    [Fact]
    public async Task Un_titulo_invalido_no_guarda_nada()
    {
        var cliente = await AutenticarAsync();
        var id = await CrearTicketAsync(cliente, "Este título sí es válido");

        var respuesta = await cliente.PatchAsJsonAsync($"/api/v1/tickets/{id}",
            new { Title = "abc", Status = "Resolved" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "un título corto es un dato mal formado, no un ticket que no existe");

        var ticket = await LeerAsync(cliente, id);
        ticket.GetProperty("title").GetString().Should().Be("Este título sí es válido");
        ticket.GetProperty("status").GetString().Should().Be("Open",
            "el estado viajaba en la misma petición: si se hubiera aplicado antes de validar el "
            + "título, el ticket quedaría medio guardado");
    }

    /// <summary>Editar la ficha entera guarda los cuatro campos de una vez.</summary>
    [Fact]
    public async Task Editar_la_ficha_guarda_titulo_descripcion_y_prioridad()
    {
        var cliente = await AutenticarAsync();
        var id = await CrearTicketAsync(cliente, "Ficha por editar desde la pantalla");

        var respuesta = await cliente.PatchAsJsonAsync($"/api/v1/tickets/{id}", new
        {
            Title = "Ficha ya editada desde la pantalla",
            Description = "Descripción nueva",
            Priority = "High",
            Status = "PendingInfo"
        });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var ticket = await LeerAsync(cliente, id);
        ticket.GetProperty("title").GetString().Should().Be("Ficha ya editada desde la pantalla");
        ticket.GetProperty("description").GetString().Should().Be("Descripción nueva");
        ticket.GetProperty("priority").GetString().Should().Be("High");
        ticket.GetProperty("status").GetString().Should().Be("PendingInfo");
    }
}
