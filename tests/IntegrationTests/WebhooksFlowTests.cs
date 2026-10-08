using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Los webhooks de punta a punta: suscribirse a eventos concretos, que lleguen firmados al
/// servidor de fuera, que no lleven secretos y que se reintenten si fallan.
///
/// Lo que había antes: una suscripción por evento, la entrega dentro de la propia petición sin
/// reintentos ni registro, el comando entero como contenido —contraseña incluida— y la gestión
/// abierta a cualquiera con sesión. El servidor de fuera es <see cref="WebhookReceiver"/>.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class WebhooksFlowTests(CrmApiFactory factory)
{
    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private Task<HttpClient> AdminAsync() => LoginAsync("admin@acme.com", "admin123");

    /// <summary>Una URL distinta por prueba, para reconocer sus envíos entre los de las demás.</summary>
    private static string UniqueUrl(string? suffix = null) => $"https://hooks.example.test/{Guid.NewGuid():N}{suffix}";

    private static async Task<(Guid Id, string Secret)> SubscribeAsync(HttpClient admin, string url, params string[] events)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/webhooks", new { name = "Prueba", url, eventTypes = events });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("subscription").GetProperty("id").GetGuid(), body.GetProperty("secret").GetString()!);
    }

    private static string Sign(string secret, string timestamp, string body)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();

    [Fact]
    public async Task Only_admins_manage_webhooks()
    {
        var admin = await AdminAsync();
        var email = $"miembro.{Guid.NewGuid():N}@acme.com";
        (await admin.PostAsJsonAsync("/api/v1/users", new { Name = "Miembro", Email = email, Password = "Miembro2026!x", Role = "Member" }))
            .EnsureSuccessStatusCode();
        var member = await LoginAsync(email, "Miembro2026!x");

        (await member.GetAsync("/api/v1/webhooks")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PostAsJsonAsync("/api/v1/webhooks", new { name = "Fuga", url = UniqueUrl(), eventTypes = new[] { "task.created" } }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "con un webhook, quien lo crea recibe los datos de toda la organización en su servidor");
    }

    [Fact]
    public async Task The_catalog_offers_every_area_that_was_asked_for()
    {
        var admin = await AdminAsync();

        var events = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/webhooks/events")).EnumerateArray().ToList();

        events.Select(e => e.GetProperty("category").GetString()).Distinct().Should().Contain(
            ["tasks", "tickets", "projects", "users", "teams", "reports", "documents", "notifications"]);
    }

    [Theory]
    [InlineData(new string[0], "sin eventos: nunca «todo» por defecto")]
    [InlineData(new[] { "*" }, "sin comodines")]
    [InlineData(new[] { "task.exploded" }, "un evento que no existe")]
    public async Task A_subscription_must_name_real_events(string[] events, string because)
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/webhooks", new { name = "Mal", url = UniqueUrl(), eventTypes = events });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, because);
    }

    [Theory]
    [InlineData("https://127.0.0.1/hook")]
    [InlineData("https://localhost/hook")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://10.0.0.5/hook")]
    [InlineData("http://hooks.example.test/hook")]
    public async Task Internal_or_insecure_urls_are_rejected(string url)
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/v1/webhooks", new { name = "Dentro", url, eventTypes = new[] { "task.created" } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "el servidor no puede hacer de puente hacia sus propios servicios internos");
    }

    [Fact]
    public async Task A_subscribed_event_arrives_signed_with_what_was_created()
    {
        var admin = await AdminAsync();
        var url = UniqueUrl();
        var (_, secret) = await SubscribeAsync(admin, url, "task.created");

        var creation = await admin.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = await TestProjects.CreateAsync(admin),
            title = "Tarea que avisa por webhook",
            description = "Creada por las pruebas de webhooks",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 1m,
            dueDate = "2026-12-01",
        });
        creation.EnsureSuccessStatusCode();
        var taskId = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var received = await factory.Webhooks.WaitForAsync(r => r.Url.ToString() == url);

        received.Headers["X-Webhook-Event"].Should().Be("task.created");
        received.Headers["X-Webhook-Signature"].Should().Be("sha256=" + Sign(secret, received.Headers["X-Webhook-Timestamp"], received.Body),
            "el receptor comprueba con el secreto que el envío es nuestro y que nadie lo tocó");

        var body = JsonDocument.Parse(received.Body).RootElement;
        body.GetProperty("event").GetString().Should().Be("task.created");
        body.GetProperty("id").GetString().Should().Be(received.Headers["X-Webhook-Delivery"]);
        body.GetProperty("data").GetProperty("result").GetProperty("id").GetGuid().Should().Be(taskId,
            "sin el identificador de lo creado, el suscriptor no puede hacer nada con el evento");
    }

    [Fact]
    public async Task An_event_that_was_not_chosen_does_not_arrive()
    {
        var admin = await AdminAsync();
        var url = UniqueUrl();
        await SubscribeAsync(admin, url, "project.deleted");

        (await admin.PostAsJsonAsync("/api/v1/teams", new { name = "Equipo sin aviso", description = "", memberIds = Array.Empty<Guid>() }))
            .EnsureSuccessStatusCode();
        var probe = await TestProjects.CreateAsync(admin);

        await Task.Delay(TimeSpan.FromSeconds(3));
        factory.Webhooks.All.Should().NotContain(r => r.Url.ToString() == url,
            $"sólo se suscribió a project.deleted, y no se ha borrado ningún proyecto (se creó {probe})");
    }

    /// <summary>El comando de crear un usuario lleva la contraseña en claro, y se mandaba tal cual.</summary>
    [Fact]
    public async Task A_new_user_arrives_without_its_password()
    {
        var admin = await AdminAsync();
        var url = UniqueUrl();
        await SubscribeAsync(admin, url, "user.created");

        const string password = "Contraseña2026!xyz";
        (await admin.PostAsJsonAsync("/api/v1/users", new { Name = "Nueva", Email = $"nueva.{Guid.NewGuid():N}@acme.com", Password = password, Role = "Member" }))
            .EnsureSuccessStatusCode();

        var received = await factory.Webhooks.WaitForAsync(r => r.Url.ToString() == url);
        received.Body.Should().NotContain(password).And.NotContainEquivalentOf("\"password\"");
        received.Body.Should().Contain("\"email\"", "lo demás sí viaja");
    }

    [Fact]
    public async Task Teams_and_documents_fire_their_events()
    {
        var admin = await AdminAsync();
        var url = UniqueUrl();
        await SubscribeAsync(admin, url, "team.created", "document.created");

        (await admin.PostAsJsonAsync("/api/v1/teams", new { name = "Equipo que avisa", description = "", memberIds = Array.Empty<Guid>() }))
            .EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/v1/docs", new { Title = "Documento que avisa", Description = "", Type = 0, TeamId = (Guid?)null, ProjectId = (Guid?)null }))
            .EnsureSuccessStatusCode();

        await factory.Webhooks.WaitForAsync(r => r.Url.ToString() == url && r.Headers["X-Webhook-Event"] == "team.created");
        await factory.Webhooks.WaitForAsync(r => r.Url.ToString() == url && r.Headers["X-Webhook-Event"] == "document.created");
    }

    [Fact]
    public async Task A_failed_delivery_is_recorded_and_will_be_retried()
    {
        var admin = await AdminAsync();
        var url = UniqueUrl("/fail");
        var (id, _) = await SubscribeAsync(admin, url, "team.created");

        (await admin.PostAsJsonAsync("/api/v1/teams", new { name = "Equipo con destino caído", description = "", memberIds = Array.Empty<Guid>() }))
            .EnsureSuccessStatusCode();
        await factory.Webhooks.WaitForAsync(r => r.Url.ToString() == url);

        JsonElement delivery = default;
        for (var i = 0; i < 20; i++)
        {
            var deliveries = (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/webhooks/{id}/deliveries")).EnumerateArray().ToList();
            delivery = deliveries.FirstOrDefault(d => d.GetProperty("attempts").GetInt32() > 0);
            if (delivery.ValueKind != JsonValueKind.Undefined) break;
            await Task.Delay(250);
        }

        delivery.GetProperty("status").GetString().Should().Be("Pending", "un fallo se reintenta más tarde, no se pierde");
        delivery.GetProperty("lastStatusCode").GetInt32().Should().Be(500);
        delivery.GetProperty("nextAttemptAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_test_button_sends_a_test_event()
    {
        var admin = await AdminAsync();
        var url = UniqueUrl();
        var (id, _) = await SubscribeAsync(admin, url, "ticket.created");

        (await admin.PostAsync($"/api/v1/webhooks/{id}/test", null)).EnsureSuccessStatusCode();

        var received = await factory.Webhooks.WaitForAsync(r => r.Url.ToString() == url);
        received.Headers["X-Webhook-Event"].Should().Be("webhook.test");
    }

    [Fact]
    public async Task A_regenerated_secret_replaces_the_old_one()
    {
        var admin = await AdminAsync();
        var (id, oldSecret) = await SubscribeAsync(admin, UniqueUrl(), "ticket.created");

        var regenerated = await admin.PostAsync($"/api/v1/webhooks/{id}/regenerate-secret", null);
        regenerated.EnsureSuccessStatusCode();
        var newSecret = (await regenerated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString();

        newSecret.Should().NotBe(oldSecret);
        (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/webhooks/{id}/secret")).GetProperty("secret").GetString().Should().Be(newSecret);
    }

    [Fact]
    public async Task Editing_changes_the_events_and_deactivating_stops_the_deliveries()
    {
        var admin = await AdminAsync();
        var url = UniqueUrl();
        var (id, _) = await SubscribeAsync(admin, url, "team.created");

        var update = await admin.PutAsJsonAsync($"/api/v1/webhooks/{id}", new
        {
            name = "Renombrado", url, eventTypes = new[] { "team.created", "team.deleted" }, isActive = false,
        });
        update.StatusCode.Should().Be(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());

        var read = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/webhooks/{id}");
        read.GetProperty("eventTypes").EnumerateArray().Select(e => e.GetString()).Should().BeEquivalentTo(["team.created", "team.deleted"]);
        read.GetProperty("isActive").GetBoolean().Should().BeFalse();

        (await admin.PostAsJsonAsync("/api/v1/teams", new { name = "Equipo sin destino", description = "", memberIds = Array.Empty<Guid>() }))
            .EnsureSuccessStatusCode();
        await Task.Delay(TimeSpan.FromSeconds(3));
        factory.Webhooks.All.Should().NotContain(r => r.Url.ToString() == url, "una suscripción desactivada no recibe nada");
    }
}
