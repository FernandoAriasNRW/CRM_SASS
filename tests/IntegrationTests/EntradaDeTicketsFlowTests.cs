using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Tickets que llegan desde fuera de la aplicación: el formulario de soporte de la web de un
/// cliente, o su backend, con una clave de entrada.
///
/// Sustituye al token de invitado, que se quitó porque abría la API entera. La clave sólo sirve
/// para crear tickets, y la organización sale de ella, no de lo que mande quien llama.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EntradaDeTicketsFlowTests(CrmApiFactory factory)
{
    private const string Entrada = "/api/v1/entrada/tickets";

    private async Task<HttpClient> AdministradorAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static async Task<(Guid id, string clave)> CrearClaveAsync(HttpClient admin, string nombre = "Web de soporte")
    {
        var respuesta = await admin.PostAsJsonAsync("/api/v1/tickets/claves-de-entrada", new { Nombre = nombre });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return (cuerpo.GetProperty("id").GetGuid(), cuerpo.GetProperty("clave").GetString()!);
    }

    /// <summary>Un cliente sin sesión, como la web de soporte de una organización.</summary>
    private HttpClient ClienteDeFuera(string? clave)
    {
        var cliente = factory.CreateClient();
        if (clave is not null)
            cliente.DefaultRequestHeaders.Add("X-Api-Key", clave);
        return cliente;
    }

    [Fact]
    public async Task Con_una_clave_se_abre_un_ticket_en_su_organizacion_sin_sesion()
    {
        var admin = await AdministradorAsync();
        var (_, clave) = await CrearClaveAsync(admin);

        var titulo = $"No puedo descargar la factura {Guid.NewGuid():N}";
        var respuesta = await ClienteDeFuera(clave).PostAsJsonAsync(Entrada, new
        {
            Title = titulo,
            Description = "Al pulsar en descargar no pasa nada",
            RequesterName = "Marta Cliente",
            RequesterEmail = "marta@cliente.example"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        var id = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Lo ve la organización de la clave, desde la aplicación, con quién lo pidió.
        var ticket = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}");
        ticket.GetProperty("title").GetString().Should().Be(titulo);
        ticket.GetProperty("origen").GetString().Should().Be("Externo");
        ticket.GetProperty("solicitanteNombre").GetString().Should().Be("Marta Cliente");
        ticket.GetProperty("solicitanteEmail").GetString().Should().Be("marta@cliente.example");
        ticket.GetProperty("priority").GetString().Should().Be("Medium", "sin prioridad, la media");
    }

    [Fact]
    public async Task Sin_clave_o_con_una_inventada_no_entra_nada()
    {
        var cuerpo = new { Title = "Ticket sin clave", Description = "No debería crearse" };

        (await ClienteDeFuera(null).PostAsJsonAsync(Entrada, cuerpo))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await ClienteDeFuera("tke_inventada").PostAsJsonAsync(Entrada, cuerpo))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Una_clave_revocada_deja_de_servir()
    {
        var admin = await AdministradorAsync();
        var (id, clave) = await CrearClaveAsync(admin, "Clave que se revoca");

        (await admin.DeleteAsync($"/api/v1/tickets/claves-de-entrada/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ClienteDeFuera(clave).PostAsJsonAsync(Entrada, new { Title = "Después de revocar", Description = "No entra" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var lista = await admin.GetFromJsonAsync<JsonElement>("/api/v1/tickets/claves-de-entrada");
        lista.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == id)
            .GetProperty("revocadaUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    /// <summary>
    /// La clave no se puede volver a leer: la lista enseña sólo el principio, para distinguirlas.
    /// </summary>
    [Fact]
    public async Task La_lista_de_claves_no_ensena_la_clave()
    {
        var admin = await AdministradorAsync();
        var (id, clave) = await CrearClaveAsync(admin, "Clave que no se enseña");

        var texto = await admin.GetStringAsync("/api/v1/tickets/claves-de-entrada");

        texto.Should().NotContain(clave);
        var laNuestra = JsonDocument.Parse(texto).RootElement.EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == id);
        clave.Should().StartWith(laNuestra.GetProperty("inicio").GetString());
    }

    /// <summary>La clave es la autorización para crear tickets, y nada más.</summary>
    [Fact]
    public async Task La_clave_no_abre_el_resto_de_la_api()
    {
        var admin = await AdministradorAsync();
        var (_, clave) = await CrearClaveAsync(admin);

        var cliente = ClienteDeFuera(clave);
        (await cliente.GetAsync("/api/v1/tickets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        cliente.DefaultRequestHeaders.Authorization = new("Bearer", clave);
        (await cliente.GetAsync("/api/v1/tickets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_miembro_no_puede_gestionar_claves()
    {
        var admin = await AdministradorAsync();
        var email = $"miembro.entrada.{Guid.NewGuid():N}@acme.com";
        (await admin.PostAsJsonAsync("/api/v1/users",
            new { Name = "Miembro sin claves", Email = email, Password = "Miembro2026!x", Role = "Member" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = "Miembro2026!x" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var miembro = factory.CreateClient();
        miembro.DefaultRequestHeaders.Authorization = new("Bearer", token);

        (await miembro.PostAsJsonAsync("/api/v1/tickets/claves-de-entrada", new { Nombre = "Colada" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await miembro.GetAsync("/api/v1/tickets/claves-de-entrada"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Datos_mal_formados_se_rechazan()
    {
        var admin = await AdministradorAsync();
        var (_, clave) = await CrearClaveAsync(admin);
        var cliente = ClienteDeFuera(clave);

        (await cliente.PostAsJsonAsync(Entrada, new { Title = "Prioridad rara", Description = "x", Priority = "Altisima" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await cliente.PostAsJsonAsync(Entrada, new { Title = "Email raro", Description = "x", RequesterEmail = "no-es-un-email" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await cliente.PostAsJsonAsync(Entrada, new { Title = "Sin descripcion", Description = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Se puede llamar desde el navegador en la web de un cliente, en un dominio que la aplicación
    /// no conoce. El resto de la API no.
    /// </summary>
    [Fact]
    public async Task El_navegador_de_otro_dominio_puede_llamar_a_la_entrada()
    {
        var cliente = factory.CreateClient();

        var previa = new HttpRequestMessage(HttpMethod.Options, Entrada);
        previa.Headers.Add("Origin", "https://soporte.cliente.example");
        previa.Headers.Add("Access-Control-Request-Method", "POST");
        previa.Headers.Add("Access-Control-Request-Headers", "content-type,x-api-key");

        var respuesta = await cliente.SendAsync(previa);

        respuesta.Headers.TryGetValues("Access-Control-Allow-Origin", out var origenes).Should().BeTrue();
        origenes!.Should().Contain("*");

        var alResto = new HttpRequestMessage(HttpMethod.Options, "/api/v1/tickets");
        alResto.Headers.Add("Origin", "https://soporte.cliente.example");
        alResto.Headers.Add("Access-Control-Request-Method", "GET");
        (await cliente.SendAsync(alResto)).Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
