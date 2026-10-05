using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// La checklist de una tarea, de punta a punta contra la API real.
///
/// Lo que sólo se ve aquí: que los puntos **vuelvan en el orden en que se escribieron**. El orden
/// de una checklist es del usuario, y una colección propiedad del agregado no vuelve ordenada de
/// la base: si la consulta se olvidara del ORDER BY, la lista saldría revuelta y nadie lo vería
/// en una prueba de dominio.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ChecklistFlowTests(CrmApiFactory factory)
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

    private async Task<Guid> CreateTaskAsync(HttpClient client, Guid tenantId, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId = Guid.NewGuid(),
            title = title,
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 1m,
            dueDate = "2026-12-01"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> AddAsync(HttpClient client, Guid task, string text)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/tasks/{task}/checklist", new { text = text });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<List<string>> TextsAsync(HttpClient client, Guid task)
    {
        var items = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}/checklist");
        return items.EnumerateArray().Select(p => p.GetProperty("text").GetString()!).ToList();
    }

    [Fact]
    public async Task Items_come_back_in_the_order_they_were_written()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var task = await CreateTaskAsync(client, tenantId, "Con checklist");

        foreach (var text in new[] { "Comprar", "Cocinar", "Comer", "Recoger" })
            await AddAsync(client, task, text);

        (await TextsAsync(client, task))
            .Should().ContainInOrder("Comprar", "Cocinar", "Comer", "Recoger");
    }

    [Fact]
    public async Task Checklist_progress_comes_with_the_task()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var task = await CreateTaskAsync(client, tenantId, "Con progreso");
        var one = await AddAsync(client, task, "Uno");
        await AddAsync(client, task, "Dos");
        await AddAsync(client, task, "Tres");

        var @checked = await client.PatchAsJsonAsync($"/api/v1/tasks/{task}/checklist/{one}", new { isDone = true });
        @checked.StatusCode.Should().Be(HttpStatusCode.OK);

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}");
        read.GetProperty("checklistTotal").GetInt32().Should().Be(3);
        read.GetProperty("checklistDone").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Renaming_an_item_does_not_uncheck_it()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var task = await CreateTaskAsync(client, tenantId, "Con typo");
        var item = await AddAsync(client, task, "Con typo");
        await client.PatchAsJsonAsync($"/api/v1/tasks/{task}/checklist/{item}", new { isDone = true });

        await client.PatchAsJsonAsync($"/api/v1/tasks/{task}/checklist/{item}", new { text = "Sin typo" });

        var items = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}/checklist");
        var single = items.EnumerateArray().Single();
        single.GetProperty("text").GetString().Should().Be("Sin typo");
        single.GetProperty("isDone").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task An_item_without_text_is_rejected()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var task = await CreateTaskAsync(client, tenantId, "Sin texto");

        var response = await client.PostAsJsonAsync($"/api/v1/tasks/{task}/checklist", new { text = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await TextsAsync(client, task)).Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_from_the_middle_keeps_the_others_in_order()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var task = await CreateTaskAsync(client, tenantId, "Con hueco");
        await AddAsync(client, task, "Primero");
        var middle = await AddAsync(client, task, "Segundo");
        await AddAsync(client, task, "Tercero");

        var deleted = await client.DeleteAsync($"/api/v1/tasks/{task}/checklist/{middle}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AddAsync(client, task, "Cuarto");

        (await TextsAsync(client, task)).Should().ContainInOrder("Primero", "Tercero", "Cuarto");
    }

    [Fact]
    public async Task Touching_a_missing_item_is_rejected()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var task = await CreateTaskAsync(client, tenantId, "Vacía");

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{task}/checklist/{Guid.NewGuid()}", new { isDone = true });
        var deleted = await client.DeleteAsync($"/api/v1/tasks/{task}/checklist/{Guid.NewGuid()}");

        patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        deleted.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
