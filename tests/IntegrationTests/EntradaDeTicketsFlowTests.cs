using System.Net;
using System.Net.Http.Headers;
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
///
/// Obligatorio: asunto, mensaje, nombre, email, teléfono y empresa. Opcional: adjuntos (imágenes
/// o vídeos), clasificación, etiquetas, equipo, estado y prioridad.
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

    /// <summary>Lo mínimo que acepta la entrada: los seis obligatorios.</summary>
    private static Dictionary<string, object?> Minimo(string? titulo = null) => new()
    {
        ["title"] = titulo ?? $"No puedo descargar la factura {Guid.NewGuid():N}",
        ["description"] = "Al pulsar en descargar no pasa nada",
        ["requesterName"] = "Marta Cliente",
        ["requesterEmail"] = "marta@cliente.example",
        ["requesterPhone"] = "+34 600 000 000",
        ["requesterCompany"] = "Cliente S.L.",
    };

    private static async Task<Guid> IdCreadoAsync(HttpResponseMessage respuesta)
    {
        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Con_una_clave_se_abre_un_ticket_en_su_organizacion_sin_sesion()
    {
        var admin = await AdministradorAsync();
        var (_, clave) = await CrearClaveAsync(admin);
        var cuerpo = Minimo();

        var id = await IdCreadoAsync(await ClienteDeFuera(clave).PostAsJsonAsync(Entrada, cuerpo));

        // Lo ve la organización de la clave, desde la aplicación, con los datos de contacto.
        var ticket = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}");
        ticket.GetProperty("title").GetString().Should().Be((string)cuerpo["title"]!);
        ticket.GetProperty("origen").GetString().Should().Be("Externo");
        ticket.GetProperty("solicitanteNombre").GetString().Should().Be("Marta Cliente");
        ticket.GetProperty("solicitanteEmail").GetString().Should().Be("marta@cliente.example");
        ticket.GetProperty("solicitanteTelefono").GetString().Should().Be("+34 600 000 000");
        ticket.GetProperty("solicitanteEmpresa").GetString().Should().Be("Cliente S.L.");
        ticket.GetProperty("priority").GetString().Should().Be("Medium", "sin prioridad, la media");
        ticket.GetProperty("status").GetString().Should().Be("Open");
    }

    /// <summary>Todos los que faltan a la vez: quien integra arregla la lista de una pasada.</summary>
    [Fact]
    public async Task Sin_los_obligatorios_no_se_crea_y_se_dice_cuales_faltan()
    {
        var admin = await AdministradorAsync();
        var (_, clave) = await CrearClaveAsync(admin);

        var respuesta = await ClienteDeFuera(clave).PostAsJsonAsync(Entrada,
            new { title = "Sólo el asunto", description = "Y el mensaje" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var texto = await respuesta.Content.ReadAsStringAsync();
        texto.Should().Contain("requesterName").And.Contain("requesterEmail")
            .And.Contain("requesterPhone").And.Contain("requesterCompany");
    }

    [Fact]
    public async Task Los_opcionales_se_guardan_si_llegan()
    {
        var admin = await AdministradorAsync();
        var (_, clave) = await CrearClaveAsync(admin);
        var equipo = Guid.NewGuid();

        var cuerpo = Minimo();
        cuerpo["priority"] = "High";
        cuerpo["status"] = "InProgress";
        cuerpo["classification"] = "Facturación";
        cuerpo["teamId"] = equipo;
        cuerpo["tags"] = new[] { "billing", "Urgent", "billing" };

        var id = await IdCreadoAsync(await ClienteDeFuera(clave).PostAsJsonAsync(Entrada, cuerpo));

        var ticket = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}");
        ticket.GetProperty("priority").GetString().Should().Be("High");
        ticket.GetProperty("status").GetString().Should().Be("InProgress");
        ticket.GetProperty("clasificacion").GetString().Should().Be("Facturación");
        ticket.GetProperty("teamId").GetGuid().Should().Be(equipo);
        ticket.GetProperty("etiquetas").GetString().Should().Be("billing,urgent", "sin repetidas y en minúsculas");
    }

    /// <summary>
    /// Un formulario con adjuntos: varias imágenes o vídeos, en multipart. Quedan en el ticket y se
    /// ven desde la aplicación.
    /// </summary>
    [Fact]
    public async Task Un_formulario_con_varios_adjuntos_los_guarda_en_el_ticket()
    {
        var admin = await AdministradorAsync();
        var (_, clave) = await CrearClaveAsync(admin);

        using var formulario = new MultipartFormDataContent();
        foreach (var (campo, valor) in Minimo())
            formulario.Add(new StringContent(valor!.ToString()!), campo);
        formulario.Add(new StringContent("billing,bug"), "tags");
        formulario.Add(Fichero("captura.png", "image/png", 2048), "attachments", "captura.png");
        formulario.Add(Fichero("grabacion.mp4", "video/mp4", 4096), "attachments", "grabacion.mp4");

        var respuesta = await ClienteDeFuera(clave).PostAsync(Entrada, formulario);
        var id = await IdCreadoAsync(respuesta);

        var adjuntos = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}/adjuntos");
        adjuntos.EnumerateArray().Select(a => a.GetProperty("nombre").GetString())
            .Should().BeEquivalentTo("captura.png", "grabacion.mp4");
        adjuntos.EnumerateArray().Should().OnlyContain(a => a.GetProperty("desdeFuera").GetBoolean());

        var ticket = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}");
        ticket.GetProperty("etiquetas").GetString().Should().Be("billing,bug");
    }

    /// <summary>Sólo imágenes y vídeos: un ejecutable disfrazado no entra, y el ticket tampoco.</summary>
    [Fact]
    public async Task Un_adjunto_que_no_es_imagen_ni_video_se_rechaza_entero()
    {
        var admin = await AdministradorAsync();
        var (_, clave) = await CrearClaveAsync(admin);
        var titulo = $"Con adjunto falso {Guid.NewGuid():N}";

        using var formulario = new MultipartFormDataContent();
        foreach (var (campo, valor) in Minimo(titulo))
            formulario.Add(new StringContent(valor!.ToString()!), campo);
        formulario.Add(Fichero("factura.exe", "image/png", 1024), "attachments", "factura.exe");

        var respuesta = await ClienteDeFuera(clave).PostAsync(Entrada, formulario);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("factura.exe");

        var lista = await admin.GetStringAsync($"/api/v1/tickets?pageSize=5&search={Uri.EscapeDataString(titulo)}");
        lista.Should().NotContain(titulo);
    }

    [Fact]
    public async Task Sin_clave_o_con_una_inventada_no_entra_nada()
    {
        (await ClienteDeFuera(null).PostAsJsonAsync(Entrada, Minimo()))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await ClienteDeFuera("tke_inventada").PostAsJsonAsync(Entrada, Minimo()))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Una_clave_revocada_deja_de_servir()
    {
        var admin = await AdministradorAsync();
        var (id, clave) = await CrearClaveAsync(admin, "Clave que se revoca");

        (await admin.DeleteAsync($"/api/v1/tickets/claves-de-entrada/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ClienteDeFuera(clave).PostAsJsonAsync(Entrada, Minimo()))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var lista = await admin.GetFromJsonAsync<JsonElement>("/api/v1/tickets/claves-de-entrada");
        lista.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == id)
            .GetProperty("revocadaUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    /// <summary>La clave no se puede volver a leer: la lista enseña sólo el principio.</summary>
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

        var prioridadRara = Minimo();
        prioridadRara["priority"] = "Altisima";
        (await cliente.PostAsJsonAsync(Entrada, prioridadRara)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var emailRaro = Minimo();
        emailRaro["requesterEmail"] = "no-es-un-email";
        (await cliente.PostAsJsonAsync(Entrada, emailRaro)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var estadoRaro = Minimo();
        estadoRaro["status"] = "Perdido";
        (await cliente.PostAsJsonAsync(Entrada, estadoRaro)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    /// <summary>
    /// Desde la aplicación: adjuntar en la ficha y guardar clasificación, equipo y etiquetas.
    /// Las etiquetas de la ficha se mandaban y no se guardaban nunca.
    /// </summary>
    [Fact]
    public async Task Desde_la_aplicacion_se_adjunta_y_se_guardan_las_etiquetas()
    {
        var admin = await AdministradorAsync();
        var creado = await admin.PostAsJsonAsync("/api/v1/tickets",
            new { Title = "Ticket desde la aplicación", Description = "Con adjuntos", Priority = "Low" });
        var id = await IdCreadoAsync(creado);

        using var formulario = new MultipartFormDataContent();
        formulario.Add(Fichero("pantalla.jpg", "image/jpeg", 512), "attachments", "pantalla.jpg");
        (await admin.PostAsync($"/api/v1/tickets/{id}/adjuntos", formulario))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var adjuntos = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}/adjuntos");
        adjuntos.EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("desdeFuera").GetBoolean().Should().BeFalse();

        (await admin.PatchAsJsonAsync($"/api/v1/tickets/{id}",
            new { Tags = new[] { "billing", "bug" }, Classification = "Acceso" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var ticket = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}");
        ticket.GetProperty("etiquetas").GetString().Should().Be("billing,bug");
        ticket.GetProperty("clasificacion").GetString().Should().Be("Acceso");
        ticket.GetProperty("title").GetString().Should().Be("Ticket desde la aplicación", "lo que no se manda no se toca");
    }

    private static ByteArrayContent Fichero(string nombre, string tipo, int bytes)
    {
        var contenido = new ByteArrayContent(Enumerable.Repeat((byte)7, bytes).ToArray());
        contenido.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        return contenido;
    }
}
