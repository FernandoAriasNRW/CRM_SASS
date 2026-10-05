using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Subtareas de punta a punta contra la API real.
///
/// Lo que sólo se ve aquí y no en las pruebas unitarias:
///
/// 1. Que las listas devuelvan **sólo tareas de primer nivel** por defecto. Es una condición
///    que se aplica en SQL, y si se colara mal el tablero se llenaría de subtareas y el total
///    de la paginación dejaría de significar «tareas».
/// 2. Que el **progreso del padre** —cuántas subtareas tiene y cuántas están completadas— lo
///    calcule la base con subconsultas correlacionadas. Un recuento en memoria daría bien con
///    tres subtareas y mal en cuanto hubiera más que tamaño de página.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SubtaskFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<(HttpClient client, Guid tenantId)> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var body = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var payload = body.PadRight(body.Length + (4 - body.Length % 4) % 4, '=');
        var tenantId = Guid.Parse(JsonDocument.Parse(Convert.FromBase64String(payload))
            .RootElement.GetProperty("tenantId").GetString()!);

        return (client, tenantId);
    }

    private static object TaskBody(Guid tenantId, Guid projectId, string title, Guid? parent) => new
    {
        tenantId,
        createdById = Guid.NewGuid(),
        projectId,
        title = title,
        description = "creada por las pruebas de integración",
        assigneeId = Guid.NewGuid(),
        estimatedHours = 1m,
        dueDate = "2026-12-01",
        parentTaskId = parent
    };

    private async Task<Guid> CreateAsync(HttpClient client, Guid tenantId, Guid projectId, string title, Guid? parent = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", TaskBody(tenantId, projectId, title, parent));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client, Guid id)
        => await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{id}");

    [Fact]
    public async Task A_subtask_keeps_its_parent_and_returns_it()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var projectId = Guid.NewGuid();
        var parent = await CreateAsync(client, tenantId, projectId, "Padre");

        var child = await CreateAsync(client, tenantId, projectId, "Hija", parent);

        (await ReadAsync(client, child)).GetProperty("parentTaskId").GetGuid().Should().Be(parent);
    }

    [Fact]
    public async Task Lists_return_only_top_level_tasks()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var projectId = Guid.NewGuid();
        var parent = await CreateAsync(client, tenantId, projectId, "Padre visible");
        await CreateAsync(client, tenantId, projectId, "Hija escondida", parent);

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks?projectId={projectId}&pageSize=200");
        var titles = page.GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!).ToList();

        titles.Should().ContainSingle().Which.Should().Be("Padre visible");
        page.GetProperty("totalCount").GetInt32().Should().Be(1,
            "el total de la paginación cuenta tareas, no subtareas");
    }

    [Fact]
    public async Task Subtasks_are_requested_by_their_parent()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var projectId = Guid.NewGuid();
        var parent = await CreateAsync(client, tenantId, projectId, "Padre");
        await CreateAsync(client, tenantId, projectId, "Hija 1", parent);
        await CreateAsync(client, tenantId, projectId, "Hija 2", parent);

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{parent}/subtasks");

        page.GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!)
            .Should().BeEquivalentTo(["Hija 1", "Hija 2"]);
    }

    [Fact]
    public async Task Parent_progress_counts_completed_subtasks()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var projectId = Guid.NewGuid();
        var parent = await CreateAsync(client, tenantId, projectId, "Padre con progreso");
        var child1 = await CreateAsync(client, tenantId, projectId, "Hija 1", parent);
        await CreateAsync(client, tenantId, projectId, "Hija 2", parent);
        await CreateAsync(client, tenantId, projectId, "Hija 3", parent);

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{child1}", new { status = "Done" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var read = await ReadAsync(client, parent);

        read.GetProperty("subtaskCount").GetInt32().Should().Be(3);
        read.GetProperty("completedSubtaskCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task A_subtask_cannot_have_subtasks()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var projectId = Guid.NewGuid();
        var parent = await CreateAsync(client, tenantId, projectId, "Padre");
        var child = await CreateAsync(client, tenantId, projectId, "Hija", parent);

        var response = await client.PostAsJsonAsync("/api/v1/tasks",
            TaskBody(tenantId, projectId, "Nieta", child));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("un solo nivel");
    }

    [Fact]
    public async Task A_task_can_be_reparented_and_detached_later()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var projectId = Guid.NewGuid();
        var parent = await CreateAsync(client, tenantId, projectId, "Padre");
        var loose = await CreateAsync(client, tenantId, projectId, "Suelta");

        var reparent = await client.PatchAsJsonAsync($"/api/v1/tasks/{loose}/parent", new { parentTaskId = parent });
        reparent.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync(client, loose)).GetProperty("parentTaskId").GetGuid().Should().Be(parent);

        var detach = await client.PatchAsJsonAsync($"/api/v1/tasks/{loose}/parent", new { parentTaskId = (Guid?)null });
        detach.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await ReadAsync(client, loose);
        after.TryGetProperty("parentTaskId", out var value).Should().BeTrue();
        value.ValueKind.Should().Be(JsonValueKind.Null, "desligar deja la tarea de primer nivel");
    }

    [Fact]
    public async Task A_task_with_subtasks_cannot_become_a_subtask()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var projectId = Guid.NewGuid();
        var withChildren = await CreateAsync(client, tenantId, projectId, "Con hijas");
        await CreateAsync(client, tenantId, projectId, "Hija", withChildren);
        var other = await CreateAsync(client, tenantId, projectId, "Otra");

        var response = await client.PatchAsJsonAsync($"/api/v1/tasks/{withChildren}/parent", new { parentTaskId = other });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("no puede convertirse en subtarea");
    }
}
