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
/// o vídeos). Nada más: prioridad, estado, clasificación, equipo y etiquetas se deciden dentro, y
/// si una integración los manda se rechaza la petición nombrándolos.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TicketIntakeFlowTests(CrmApiFactory factory)
{
    private const string Intake = "/api/v1/ticket-intake";

    private async Task<HttpClient> AdminAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<(Guid id, string key)> CreateKeyAsync(HttpClient admin, string name = "Web de soporte")
    {
        var response = await admin.PostAsJsonAsync("/api/v1/tickets/intake-keys", new { Name = name });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("id").GetGuid(), body.GetProperty("key").GetString()!);
    }

    /// <summary>Un cliente sin sesión, como la web de soporte de una organización.</summary>
    private HttpClient ExternalClient(string? key)
    {
        var client = factory.CreateClient();
        if (key is not null)
            client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    /// <summary>Lo mínimo que acepta la entrada: los seis obligatorios.</summary>
    private static Dictionary<string, object?> Minimal(string? title = null) => new()
    {
        ["title"] = title ?? $"No puedo descargar la factura {Guid.NewGuid():N}",
        ["description"] = "Al pulsar en descargar no pasa nada",
        ["requesterName"] = "Marta Cliente",
        ["requesterEmail"] = "marta@cliente.example",
        ["requesterPhone"] = "+34 600 000 000",
        ["requesterCompany"] = "Cliente S.L.",
    };

    private static async Task<Guid> CreatedIdAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task With_a_key_a_ticket_opens_in_its_organisation_without_session()
    {
        var admin = await AdminAsync();
        var (_, key) = await CreateKeyAsync(admin);
        var body = Minimal();

        var id = await CreatedIdAsync(await ExternalClient(key).PostAsJsonAsync(Intake, body));

        // Lo ve la organización de la clave, desde la aplicación, con los datos de contacto.
        var ticket = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}");
        ticket.GetProperty("title").GetString().Should().Be((string)body["title"]!);
        ticket.GetProperty("source").GetString().Should().Be("External");
        ticket.GetProperty("requesterName").GetString().Should().Be("Marta Cliente");
        ticket.GetProperty("requesterEmail").GetString().Should().Be("marta@cliente.example");
        ticket.GetProperty("requesterPhone").GetString().Should().Be("+34 600 000 000");
        ticket.GetProperty("requesterCompany").GetString().Should().Be("Cliente S.L.");
        ticket.GetProperty("priority").GetString().Should().Be("Medium", "sin prioridad, la media");
        ticket.GetProperty("status").GetString().Should().Be("Open");
    }

    /// <summary>Todos los que faltan a la vez: quien integra arregla la lista de una pasada.</summary>
    [Fact]
    public async Task Without_required_fields_nothing_is_created_and_it_lists_them()
    {
        var admin = await AdminAsync();
        var (_, key) = await CreateKeyAsync(admin);

        var response = await ExternalClient(key).PostAsJsonAsync(Intake,
            new { title = "Sólo el asunto", description = "Y el mensaje" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("requesterName").And.Contain("requesterEmail")
            .And.Contain("requesterPhone").And.Contain("requesterCompany");
    }

    [Fact]
    public async Task A_ticket_from_outside_starts_open_with_medium_priority_and_no_tags()
    {
        var admin = await AdminAsync();
        var (_, key) = await CreateKeyAsync(admin);

        var id = await CreatedIdAsync(await ExternalClient(key).PostAsJsonAsync(Intake, Minimal()));

        var ticket = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}");
        ticket.GetProperty("priority").GetString().Should().Be("Medium");
        ticket.GetProperty("status").GetString().Should().Be("Open");
        ticket.GetProperty("tagIds").EnumerateArray().Should().BeEmpty();
    }

    /// <summary>
    /// Una integración de antes que siga mandando prioridad, estado, clasificación, equipo o
    /// etiquetas recibe un 400 que los nombra, y no se crea nada: ignorarlos le haría creer que se
    /// aplicaron.
    /// </summary>
    [Theory]
    [InlineData("priority", "High")]
    [InlineData("status", "InProgress")]
    [InlineData("classification", "Facturación")]
    [InlineData("teamId", "7d4f3c1e-0000-0000-0000-000000000001")]
    [InlineData("tags", "billing")]
    public async Task A_retired_field_in_json_is_rejected_by_name(string field, string value)
    {
        var admin = await AdminAsync();
        var (_, key) = await CreateKeyAsync(admin);
        var title = $"Con campo retirado {Guid.NewGuid():N}";
        var body = Minimal(title);
        body[field] = value;

        var response = await ExternalClient(key).PostAsJsonAsync(Intake, body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(field);

        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets?search={Uri.EscapeDataString(title)}");
        list.GetRawText().Should().NotContain(title, "no se crea el ticket");
    }

    [Fact]
    public async Task A_retired_field_in_a_form_is_rejected_too()
    {
        var admin = await AdminAsync();
        var (_, key) = await CreateKeyAsync(admin);

        using var form = new MultipartFormDataContent();
        foreach (var (field, value) in Minimal())
            form.Add(new StringContent(value!.ToString()!), field);
        form.Add(new StringContent("billing,bug"), "tags");

        var response = await ExternalClient(key).PostAsync(Intake, form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("tags");
    }

    /// <summary>
    /// Un formulario con adjuntos: varias imágenes o vídeos, en multipart. Quedan en el ticket y se
    /// ven desde la aplicación.
    /// </summary>
    [Fact]
    public async Task A_form_with_several_attachments_saves_them_on_the_ticket()
    {
        var admin = await AdminAsync();
        var (_, key) = await CreateKeyAsync(admin);

        using var form = new MultipartFormDataContent();
        foreach (var (field, value) in Minimal())
            form.Add(new StringContent(value!.ToString()!), field);
        form.Add(File("captura.png", "image/png", 2048), "attachments", "captura.png");
        form.Add(File("grabacion.mp4", "video/mp4", 4096), "attachments", "grabacion.mp4");

        var response = await ExternalClient(key).PostAsync(Intake, form);
        var id = await CreatedIdAsync(response);

        var attachments = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}/attachments");
        attachments.EnumerateArray().Select(a => a.GetProperty("name").GetString())
            .Should().BeEquivalentTo("captura.png", "grabacion.mp4");
        attachments.EnumerateArray().Should().OnlyContain(a => a.GetProperty("fromExternal").GetBoolean());
    }

    /// <summary>Sólo imágenes y vídeos: un ejecutable disfrazado no entra, y el ticket tampoco.</summary>
    [Fact]
    public async Task An_attachment_that_is_not_an_image_or_video_rejects_the_whole_request()
    {
        var admin = await AdminAsync();
        var (_, key) = await CreateKeyAsync(admin);
        var title = $"Con adjunto falso {Guid.NewGuid():N}";

        using var form = new MultipartFormDataContent();
        foreach (var (field, value) in Minimal(title))
            form.Add(new StringContent(value!.ToString()!), field);
        form.Add(File("factura.exe", "image/png", 1024), "attachments", "factura.exe");

        var response = await ExternalClient(key).PostAsync(Intake, form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("factura.exe");

        var list = await admin.GetStringAsync($"/api/v1/tickets?pageSize=5&search={Uri.EscapeDataString(title)}");
        list.Should().NotContain(title);
    }

    [Fact]
    public async Task Without_a_key_or_with_a_fake_one_nothing_gets_in()
    {
        (await ExternalClient(null).PostAsJsonAsync(Intake, Minimal()))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await ExternalClient("tke_inventada").PostAsJsonAsync(Intake, Minimal()))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_revoked_key_stops_working()
    {
        var admin = await AdminAsync();
        var (id, key) = await CreateKeyAsync(admin, "Clave que se revoca");

        (await admin.DeleteAsync($"/api/v1/tickets/intake-keys/{id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ExternalClient(key).PostAsJsonAsync(Intake, Minimal()))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var list = await admin.GetFromJsonAsync<JsonElement>("/api/v1/tickets/intake-keys");
        list.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == id)
            .GetProperty("revokedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    /// <summary>La clave no se puede volver a leer: la lista enseña sólo el principio.</summary>
    [Fact]
    public async Task The_key_list_does_not_show_the_key()
    {
        var admin = await AdminAsync();
        var (id, key) = await CreateKeyAsync(admin, "Clave que no se enseña");

        var text = await admin.GetStringAsync("/api/v1/tickets/intake-keys");

        text.Should().NotContain(key);
        var ours = JsonDocument.Parse(text).RootElement.EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == id);
        key.Should().StartWith(ours.GetProperty("prefix").GetString());
    }

    /// <summary>La clave es la autorización para crear tickets, y nada más.</summary>
    [Fact]
    public async Task The_key_does_not_open_the_rest_of_the_api()
    {
        var admin = await AdminAsync();
        var (_, key) = await CreateKeyAsync(admin);

        var client = ExternalClient(key);
        (await client.GetAsync("/api/v1/tickets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new("Bearer", key);
        (await client.GetAsync("/api/v1/tickets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_member_cannot_manage_keys()
    {
        var admin = await AdminAsync();
        var email = $"miembro.entrada.{Guid.NewGuid():N}@acme.com";
        (await admin.PostAsJsonAsync("/api/v1/users",
            new { Name = "Miembro sin claves", Email = email, Password = "Miembro2026!x", Role = "Member" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = "Miembro2026!x" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var member = factory.CreateClient();
        member.DefaultRequestHeaders.Authorization = new("Bearer", token);

        (await member.PostAsJsonAsync("/api/v1/tickets/intake-keys", new { Name = "Colada" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.GetAsync("/api/v1/tickets/intake-keys"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Malformed_data_is_rejected()
    {
        var admin = await AdminAsync();
        var (_, key) = await CreateKeyAsync(admin);
        var client = ExternalClient(key);

        var oddPriority = Minimal();
        oddPriority["priority"] = "Altisima";
        (await client.PostAsJsonAsync(Intake, oddPriority)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var oddEmail = Minimal();
        oddEmail["requesterEmail"] = "no-es-un-email";
        (await client.PostAsJsonAsync(Intake, oddEmail)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var oddStatus = Minimal();
        oddStatus["status"] = "Perdido";
        (await client.PostAsJsonAsync(Intake, oddStatus)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Se puede llamar desde el navegador en la web de un cliente, en un dominio que la aplicación
    /// no conoce. El resto de la API no.
    /// </summary>
    [Fact]
    public async Task A_browser_from_another_domain_can_call_the_intake()
    {
        var client = factory.CreateClient();

        var preview = new HttpRequestMessage(HttpMethod.Options, Intake);
        preview.Headers.Add("Origin", "https://soporte.cliente.example");
        preview.Headers.Add("Access-Control-Request-Method", "POST");
        preview.Headers.Add("Access-Control-Request-Headers", "content-type,x-api-key");

        var response = await client.SendAsync(preview);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).Should().BeTrue();
        origins!.Should().Contain("*");

        var toTheRest = new HttpRequestMessage(HttpMethod.Options, "/api/v1/tickets");
        toTheRest.Headers.Add("Origin", "https://soporte.cliente.example");
        toTheRest.Headers.Add("Access-Control-Request-Method", "GET");
        (await client.SendAsync(toTheRest)).Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    /// <summary>
    /// Desde la aplicación: adjuntar en la ficha y guardar clasificación y etiquetas. Las etiquetas
    /// son las del módulo de etiquetas, por id.
    /// </summary>
    [Fact]
    public async Task From_the_app_attachments_and_tags_are_saved()
    {
        var admin = await AdminAsync();
        var created = await admin.PostAsJsonAsync("/api/v1/tickets",
            new { Title = "Ticket desde la aplicación", Description = "Con adjuntos", Priority = "Low" });
        var id = await CreatedIdAsync(created);

        using var form = new MultipartFormDataContent();
        form.Add(File("pantalla.jpg", "image/jpeg", 512), "attachments", "pantalla.jpg");
        (await admin.PostAsync($"/api/v1/tickets/{id}/attachments", form))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var attachments = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}/attachments");
        attachments.EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("fromExternal").GetBoolean().Should().BeFalse();

        var tags = await admin.GetFromJsonAsync<JsonElement>("/api/v1/tags");
        Guid Tag(string key) => tags.EnumerateArray()
            .Single(t => t.GetProperty("builtInKey").GetString() == key).GetProperty("id").GetGuid();
        var facturacion = Tag("billing");
        var bug = Tag("bug");

        var saved = await admin.PatchAsJsonAsync($"/api/v1/tickets/{id}",
            new { TagIds = new[] { facturacion, bug, facturacion }, Classification = "Acceso" });
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());

        var ticket = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{id}");
        ticket.GetProperty("tagIds").EnumerateArray().Select(t => t.GetGuid())
            .Should().Equal([facturacion, bug], "sin repetidas y en el orden en que llegan");
        ticket.GetProperty("classification").GetString().Should().Be("Acceso");
        ticket.GetProperty("title").GetString().Should().Be("Ticket desde la aplicación", "lo que no se manda no se toca");
    }

    private static ByteArrayContent File(string name, string type, int bytes)
    {
        var content = new ByteArrayContent(Enumerable.Repeat((byte)7, bytes).ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return content;
    }
}
