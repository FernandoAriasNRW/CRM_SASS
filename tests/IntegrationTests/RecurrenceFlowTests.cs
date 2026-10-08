using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkItems.Infrastructure.Recurrence;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Tareas recurrentes de punta a punta.
///
/// La prueba que de verdad importa aquí es la del generador: se ejecuta **fuera de una
/// petición**, como el worker, es decir sin usuario y por tanto sin tenant. El filtro global
/// cierra por defecto, así que un generador que no declarara `IgnoreQueryFilters` no vería ni
/// una serie y el worker daría vueltas cada hora sin crear nada **y sin dar un solo error**.
/// Eso ya pasó una vez en este proyecto con el claim del tenant, y no se detecta con pruebas de
/// dominio.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RecurrenceFlowTests(CrmApiFactory factory)
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

    private async Task<Guid> CreateTaskAsync(HttpClient client, Guid tenantId, Guid projectId, string title, DateOnly deadline)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId,
            title = title,
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 1m,
            dueDate = deadline.ToString("yyyy-MM-dd")
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Ejecuta el generador como lo hace el worker: sin usuario y sin tenant.</summary>
    private async Task<int> GenerateAsTheWorkerAsync(DateOnly today)
    {
        using var scope = factory.Services.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<RecurringTaskGenerator>();

        return await generator.GeneratePendingAsync(today);
    }

    [Fact]
    public async Task A_task_can_be_marked_recurring_and_shows_on_read()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var task = await CreateTaskAsync(client, tenantId, await TestProjects.CreateAsync(client), "Informe semanal", new DateOnly(2026, 9, 1));

        var set = await client.PutAsJsonAsync($"/api/v1/tasks/{task}/recurrence", new
        {
            frequency = "Weekly",
            interval = 1,
            nextOccurrence = "2026-09-01"
        });
        set.StatusCode.Should().Be(HttpStatusCode.OK);

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}");
        var recurrence = read.GetProperty("recurrence");
        recurrence.GetProperty("frequency").GetString().Should().Be("Weekly");
        recurrence.GetProperty("interval").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task The_generator_sees_the_series_even_without_a_user()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var project = await TestProjects.CreateAsync(client);
        var mark = $"serie-{Guid.NewGuid():N}";
        var task = await CreateTaskAsync(client, tenantId, project, mark, new DateOnly(2026, 1, 5));

        await client.PutAsJsonAsync($"/api/v1/tasks/{task}/recurrence", new
        {
            frequency = "Daily",
            interval = 1,
            nextOccurrence = "2026-01-05",
            endDate = "2026-01-07"
        });

        // Tres días pendientes: 5, 6 y 7. El worker se ejecuta sin usuario en contexto.
        var created = await GenerateAsTheWorkerAsync(new DateOnly(2026, 1, 10));

        created.Should().Be(3, "sin IgnoreQueryFilters el generador no vería la serie y devolvería 0");

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks?projectId={project}&pageSize=50");
        var limits = page.GetProperty("items").EnumerateArray()
            .Where(t => t.GetProperty("title").GetString() == mark)
            .Select(t => t.GetProperty("dueDate").GetString()!)
            .OrderBy(f => f)
            .ToList();

        // La plantilla más las tres ocurrencias.
        limits.Should().HaveCount(4);
        limits.Should().Contain(["2026-01-05", "2026-01-06", "2026-01-07"]);
    }

    [Fact]
    public async Task Generated_tasks_belong_to_their_template_tenant()
    {
        // El generador cruza tenants para leer, pero lo que escribe tiene que quedar aislado: si
        // una ocurrencia naciera con el tenant vacío, sería invisible para todo el mundo.
        var (client, tenantId) = await AuthenticateAsync();
        var project = await TestProjects.CreateAsync(client);
        var mark = $"aislada-{Guid.NewGuid():N}";
        var task = await CreateTaskAsync(client, tenantId, project, mark, new DateOnly(2026, 2, 2));

        await client.PutAsJsonAsync($"/api/v1/tasks/{task}/recurrence", new
        {
            frequency = "Daily",
            interval = 1,
            nextOccurrence = "2026-02-02",
            endDate = "2026-02-02"
        });

        (await GenerateAsTheWorkerAsync(new DateOnly(2026, 2, 5))).Should().Be(1);

        // Se consulta con el tenant del usuario: si la ocurrencia tuviera otro, no saldría.
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks?projectId={project}&pageSize=50");
        page.GetProperty("items").EnumerateArray()
            .Count(t => t.GetProperty("title").GetString() == mark)
            .Should().Be(2, "la plantilla y su ocurrencia, las dos visibles para el tenant");
    }

    [Fact]
    public async Task Generating_twice_does_not_duplicate_occurrences()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var project = await TestProjects.CreateAsync(client);
        var mark = $"idempotente-{Guid.NewGuid():N}";
        var task = await CreateTaskAsync(client, tenantId, project, mark, new DateOnly(2026, 3, 3));

        await client.PutAsJsonAsync($"/api/v1/tasks/{task}/recurrence", new
        {
            frequency = "Daily",
            interval = 1,
            nextOccurrence = "2026-03-03",
            endDate = "2026-03-04"
        });

        var first = await GenerateAsTheWorkerAsync(new DateOnly(2026, 3, 10));
        var second = await GenerateAsTheWorkerAsync(new DateOnly(2026, 3, 10));

        first.Should().Be(2);
        second.Should().Be(0, "la serie ya avanzó su próxima ocurrencia y quedó agotada");
    }

    [Fact]
    public async Task Stopping_the_repeat_stops_the_series()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var project = await TestProjects.CreateAsync(client);
        var task = await CreateTaskAsync(client, tenantId, project, $"parada-{Guid.NewGuid():N}", new DateOnly(2026, 4, 4));

        await client.PutAsJsonAsync($"/api/v1/tasks/{task}/recurrence", new
        {
            frequency = "Daily", interval = 1, nextOccurrence = "2026-04-04"
        });

        var removed = await client.DeleteAsync($"/api/v1/tasks/{task}/recurrence");
        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GenerateAsTheWorkerAsync(new DateOnly(2026, 4, 30))).Should().Be(0);
        (await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}"))
            .GetProperty("recurrence").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_made_up_frequency_is_rejected()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var task = await CreateTaskAsync(client, tenantId, await TestProjects.CreateAsync(client), "Rara", new DateOnly(2026, 5, 5));

        var response = await client.PutAsJsonAsync($"/api/v1/tasks/{task}/recurrence", new
        {
            frequency = "Trimestral", interval = 1, nextOccurrence = "2026-05-05"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Daily, Weekly o Monthly");
    }
}
