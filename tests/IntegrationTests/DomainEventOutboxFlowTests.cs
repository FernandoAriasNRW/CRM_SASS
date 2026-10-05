using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkItems.Domain.Events;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Un evento de dominio se guarda una vez en el outbox y se reparte una vez en proceso.
///
/// Hasta octubre de 2026 lo escribían dos sitios: <c>UnitOfWork.SaveChangesAndDispatchAsync</c>
/// y, otra vez, el <c>DomainEventDispatcher</c> al que llama. Crear una tarea dejaba dos filas
/// <c>TaskCreatedEvent</c> con la misma carga —el mismo <c>EventId</c>— y el worker del outbox
/// las publicaba las dos. Los manejadores de MediatR no se duplicaban, porque el reparto en
/// proceso sí era uno; la segunda prueba lo deja fijado para que arreglar una mitad no rompa la otra.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DomainEventOutboxFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<(HttpClient client, Guid tenantId)> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        return (client, TenantFromToken(token));
    }

    private static Guid TenantFromToken(string token)
    {
        var body = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var payload = body.PadRight(body.Length + (4 - body.Length % 4) % 4, '=');
        var json = JsonDocument.Parse(Convert.FromBase64String(payload));

        return Guid.Parse(json.RootElement.GetProperty("tenantId").GetString()!);
    }

    private static async Task<Guid> CreateTaskAsync(HttpClient client, Guid tenantId, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId = Guid.NewGuid(),
            title,
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 2m,
            dueDate = "2026-12-01",
            priority = "Normal"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Las filas del outbox para el <c>TaskCreatedEvent</c> de una tarea concreta.
    ///
    /// Se leen directamente de la tabla porque no hay endpoint que la enseñe. El worker puede
    /// haberlas marcado ya como procesadas, pero no las borra: contarlas vale igual.
    /// </summary>
    private async Task<List<string>> TaskCreatedPayloadsAsync(Guid taskId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var eventType = typeof(TaskCreatedEvent).FullName!;
        var marker = taskId.ToString();

        return await context.OutboxMessages
            .Where(m => m.Type == eventType && m.Payload.Contains(marker))
            .Select(m => m.Payload)
            .ToListAsync();
    }

    [Fact]
    public async Task Creating_a_task_writes_its_event_to_the_outbox_exactly_once()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var taskId = await CreateTaskAsync(client, tenantId, "Tarea para contar el outbox");

        var payloads = await TaskCreatedPayloadsAsync(taskId);

        payloads.Should().ContainSingle(
            "el UnitOfWork escribe el evento en el outbox y el dispatcher sólo lo reparte en proceso");

        var payload = JsonDocument.Parse(payloads[0]).RootElement;
        payload.GetProperty("TaskId").GetGuid().Should().Be(taskId);
    }

    /// <summary>
    /// El efecto en proceso: una automatización con disparador «tarea creada» se ejecuta una vez.
    /// Si el reparto por MediatR se duplicara, el contador de ejecuciones daría 2.
    /// </summary>
    [Fact]
    public async Task Creating_a_task_runs_its_automation_exactly_once()
    {
        var (client, tenantId) = await AuthenticateAsync();

        // Las reglas activas de otras pruebas se ejecutarían igual y alterarían la tarea.
        var existing = await client.GetFromJsonAsync<JsonElement>("/api/v1/automations");
        foreach (var rule in existing.EnumerateArray())
            await client.DeleteAsync($"/api/v1/automations/{rule.GetProperty("id").GetGuid()}");

        var marker = $"outbox-{Guid.NewGuid():N}";
        var created = await client.PostAsJsonAsync("/api/v1/automations", new
        {
            name = $"Bajar al crear {marker}",
            trigger = "TaskCreated",
            conditions = new[] { new { field = "Title", @operator = "Contains", value = marker } },
            actions = new[] { new { type = "ChangePriority", value = "Low" } },
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var ruleId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var taskId = await CreateTaskAsync(client, tenantId, $"Tarea {marker}");

        var task = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{taskId}");
        task.GetProperty("priority").GetString().Should().Be("Low", "la regla tenía que ejecutarse");

        var rules = await client.GetFromJsonAsync<JsonElement>("/api/v1/automations");
        var ruleAfter = rules.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == ruleId);
        ruleAfter.GetProperty("executionCount").GetInt32().Should().Be(1);

        (await TaskCreatedPayloadsAsync(taskId)).Should().ContainSingle();
    }
}
