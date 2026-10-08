using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Los avisos a quien le interesa: quien creó la tarea, el ticket o el proyecto, y quien lo tiene
/// asignado. Nunca a quien hizo el cambio, y siempre con sus preferencias.
///
/// Hasta ahora no avisaba nada de esto: sólo las exportaciones y las automatizaciones mandaban
/// avisos. Cada prueba crea personas de verdad, porque un aviso a un identificador que no es de la
/// organización no se manda.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class WorkNotificationsFlowTests(CrmApiFactory factory)
{
    private const string MemberPassword = "Miembro2026!x";

    private async Task<(HttpClient Client, string Token)> LoginAsync(string email, string password)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return (client, token);
    }

    private async Task<HttpClient> AdminAsync() => (await LoginAsync("admin@acme.com", "admin123")).Client;

    /// <summary>Una persona nueva de la organización, con su sesión.</summary>
    private async Task<(HttpClient Client, Guid Id, string Token)> MemberAsync(HttpClient admin, string name)
    {
        var email = $"{name.ToLowerInvariant()}.{Guid.NewGuid():N}@acme.com";
        var created = await admin.PostAsJsonAsync("/api/v1/users", new { Name = name, Email = email, Password = MemberPassword, Role = "Member" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var (client, token) = await LoginAsync(email, MemberPassword);
        return (client, id, token);
    }

    private static async Task<Guid> CreateTaskAsync(HttpClient client, Guid assignee, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = await TestProjects.CreateAsync(client),
            title,
            description = "Creada por las pruebas de avisos",
            assigneeId = assignee,
            estimatedHours = 1m,
            dueDate = "2026-12-01",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Los avisos de quien pregunta: los suyos y sólo los suyos.</summary>
    private static async Task<List<JsonElement>> NotificationsAsync(HttpClient client)
        => (await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications?pageSize=200"))
            .GetProperty("items").EnumerateArray().ToList();

    private static async Task<List<JsonElement>> AboutAsync(HttpClient client, Guid entityId, string? kind = null)
        => (await NotificationsAsync(client))
            .Where(n => n.GetProperty("entityId").ValueKind == JsonValueKind.String && n.GetProperty("entityId").GetGuid() == entityId)
            .Where(n => kind is null || n.GetProperty("kind").GetString() == kind)
            .ToList();

    [Fact]
    public async Task Whoever_is_assigned_a_task_is_told_and_the_link_takes_them_there()
    {
        var admin = await AdminAsync();
        var (ana, anaId, _) = await MemberAsync(admin, "Ana");

        var task = await CreateTaskAsync(admin, anaId, "Revisar el contrato");

        var received = await AboutAsync(ana, task, "task.assigned");
        received.Should().ContainSingle();
        received[0].GetProperty("entityType").GetString().Should().Be("Task");
        received[0].GetProperty("body").GetString().Should().Contain("Revisar el contrato");

        (await AboutAsync(admin, task)).Should().BeEmpty("quien hace algo no recibe aviso de lo que acaba de hacer");
    }

    [Fact]
    public async Task The_creator_is_told_when_the_assignee_moves_or_finishes_the_task()
    {
        var admin = await AdminAsync();
        var (ana, anaId, _) = await MemberAsync(admin, "Ana");
        var task = await CreateTaskAsync(admin, anaId, "Preparar la demo");

        (await ana.PatchAsJsonAsync($"/api/v1/tasks/{task}", new { status = "In Progress" })).EnsureSuccessStatusCode();
        (await AboutAsync(admin, task, "task.status_changed")).Should().ContainSingle()
            .Which.GetProperty("body").GetString().Should().Contain("En progreso", "el estado se dice como en la pantalla");

        (await ana.PatchAsJsonAsync($"/api/v1/tasks/{task}", new { status = "Done" })).EnsureSuccessStatusCode();
        (await AboutAsync(admin, task, "task.completed")).Should().ContainSingle();

        (await AboutAsync(ana, task, "task.status_changed")).Should().BeEmpty("lo movió ella");
    }

    [Fact]
    public async Task A_comment_reaches_the_creator_and_the_assignee_but_not_its_author()
    {
        var admin = await AdminAsync();
        var (ana, anaId, _) = await MemberAsync(admin, "Ana");
        var (luis, _, _) = await MemberAsync(admin, "Luis");
        var task = await CreateTaskAsync(admin, anaId, "Tarea comentada");

        (await luis.PostAsJsonAsync($"/api/v1/comments/Task/{task}", new { text = "Le echo un ojo mañana" })).EnsureSuccessStatusCode();

        (await AboutAsync(admin, task, "task.commented")).Should().ContainSingle();
        (await AboutAsync(ana, task, "task.commented")).Should().ContainSingle();
        (await AboutAsync(luis, task)).Should().BeEmpty();
    }

    /// <summary>
    /// Quien está mencionado recibe la mención y no además «han comentado»: es el mismo comentario.
    /// Una mención a alguien que no es de la organización no se manda a nadie.
    /// </summary>
    [Fact]
    public async Task A_mention_reaches_the_person_once()
    {
        var admin = await AdminAsync();
        var (ana, anaId, _) = await MemberAsync(admin, "Ana");
        var task = await CreateTaskAsync(admin, anaId, "Tarea con mención");

        var text = $"@[Ana](Person:{anaId}) ¿lo ves? Y también @[Nadie](Person:{Guid.NewGuid()})";
        (await admin.PostAsJsonAsync($"/api/v1/comments/Task/{task}", new { text })).EnsureSuccessStatusCode();

        var forAna = await AboutAsync(ana, task);
        forAna.Where(n => n.GetProperty("kind").GetString() == "mention").Should().ContainSingle()
            .Which.GetProperty("body").GetString().Should().StartWith("@Ana", "el aviso se lee con nombres, no con identificadores");
        forAna.Should().NotContain(n => n.GetProperty("kind").GetString() == "task.commented");
    }

    [Fact]
    public async Task A_type_turned_off_is_not_received()
    {
        var admin = await AdminAsync();
        var (ana, anaId, _) = await MemberAsync(admin, "Ana");

        (await ana.PutAsJsonAsync("/api/v1/notifications/preferences", new
        {
            emailEnabled = true, pushEnabled = false, quietHoursEnabled = false, quietHoursStart = "22:00", quietHoursEnd = "08:00",
            types = new[] { new { kind = "task.assigned", enabled = false } },
        })).EnsureSuccessStatusCode();

        var task = await CreateTaskAsync(admin, anaId, "No me avises de esta");

        (await AboutAsync(ana, task)).Should().BeEmpty("lo apagó en sus preferencias");
    }

    [Fact]
    public async Task Whoever_is_assigned_a_ticket_is_told()
    {
        var admin = await AdminAsync();
        var (ana, anaId, _) = await MemberAsync(admin, "Ana");

        var created = await admin.PostAsJsonAsync("/api/v1/tickets", new { Title = "La factura no cuadra", Description = "Hay que revisarla", Priority = "Medium" });
        created.EnsureSuccessStatusCode();
        var ticket = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await admin.PatchAsJsonAsync($"/api/v1/tickets/{ticket}", new { AssignedAgentId = anaId })).EnsureSuccessStatusCode();

        (await AboutAsync(ana, ticket, "ticket.assigned")).Should().ContainSingle();
    }

    /// <summary>
    /// Cada persona ve sus avisos y no los de las demás. La lista devolvía los de toda la
    /// organización a quien no mandara destinatario —y la pantalla no lo mandaba—, y se podía marcar
    /// como leído el aviso de otro.
    /// </summary>
    [Fact]
    public async Task Nobody_sees_or_touches_someone_elses_notifications()
    {
        var admin = await AdminAsync();
        var (ana, anaId, _) = await MemberAsync(admin, "Ana");
        var (luis, _, _) = await MemberAsync(admin, "Luis");
        var task = await CreateTaskAsync(admin, anaId, "Sólo para Ana");
        var anasNotification = (await AboutAsync(ana, task)).Single().GetProperty("id").GetGuid();

        (await NotificationsAsync(luis)).Should().NotContain(n => n.GetProperty("id").GetGuid() == anasNotification);
        (await luis.GetAsync($"/api/v1/notifications/{anasNotification}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await luis.PostAsync($"/api/v1/notifications/{anasNotification}/read", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await luis.DeleteAsync($"/api/v1/notifications/{anasNotification}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await AboutAsync(ana, task)).Single().GetProperty("status").GetString().Should().NotBe("Read");
    }

    /// <summary>El aviso aparece en la pantalla en el momento: la pantalla lo escuchaba y nadie lo mandaba.</summary>
    [Fact]
    public async Task The_notification_arrives_in_real_time()
    {
        var admin = await AdminAsync();
        var (_, anaId, anaToken) = await MemberAsync(admin, "Ana");

        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, $"hubs/notifications?access_token={anaToken}"), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, ct) =>
                    await factory.Server.CreateWebSocketClient().ConnectAsync(context.Uri, ct);
            })
            .Build();

        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<JsonElement>("notification_received", n => received.TrySetResult(n));
        await hub.StartAsync();

        var task = await CreateTaskAsync(admin, anaId, "Aviso en vivo");

        var notification = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        notification.GetProperty("entityId").GetGuid().Should().Be(task);
        notification.GetProperty("kind").GetString().Should().Be("task.assigned");
    }
}
