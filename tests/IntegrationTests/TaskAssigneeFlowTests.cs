using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Múltiples responsables de punta a punta contra la API real.
///
/// Lo que sólo se ve aquí:
///
/// 1. Que la colección **se guarde y se recupere** de verdad: es una colección propiedad del
///    agregado, mapeada a su propia tabla, y ese ida y vuelta no lo cubre ninguna prueba de
///    dominio.
/// 2. Que los **filtros** por responsable y «mis tareas» miren el conjunto y no sólo el campo
///    del principal. Un filtro que se quedara mirando el campo antiguo seguiría funcionando para
///    el principal y perdería en silencio a todos los demás.
/// 3. Que el **traspaso** de la migración dejara los datos coherentes: ninguna tarea con
///    principal puede quedarse sin su fila de responsable.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TaskAssigneeFlowTests(CrmApiFactory factory)
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

    private async Task<Guid> CreateAsync(HttpClient client, Guid tenantId, Guid projectId, string title, Guid assignee)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId,
            title = title,
            description = "creada por las pruebas de integración",
            assigneeId = assignee,
            estimatedHours = 1m,
            dueDate = "2026-12-01"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<List<Guid>> AssigneesOfAsync(HttpClient client, Guid task)
    {
        var read = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}");
        return read.GetProperty("assignees").EnumerateArray().Select(x => x.GetGuid()).ToList();
    }

    [Fact]
    public async Task A_task_created_with_an_assignee_returns_it_in_the_collection()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var who = Guid.NewGuid();

        var task = await CreateAsync(client, tenantId, await TestProjects.CreateAsync(client), "Con responsable", who);

        (await AssigneesOfAsync(client, task)).Should().ContainSingle().Which.Should().Be(who);
    }

    [Fact]
    public async Task Several_assignees_can_be_added()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var principal = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var task = await CreateAsync(client, tenantId, await TestProjects.CreateAsync(client), "En equipo", principal);

        foreach (var who in new[] { second, third })
        {
            var response = await client.PostAsJsonAsync($"/api/v1/tasks/{task}/assignees", new { userId = who });
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await AssigneesOfAsync(client, task)).Should().BeEquivalentTo([principal, second, third]);

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}");
        read.GetProperty("assigneeId").GetGuid().Should().Be(principal, "añadir gente no cambia el principal");
    }

    [Fact]
    public async Task The_same_person_is_not_added_twice()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var who = Guid.NewGuid();
        var task = await CreateAsync(client, tenantId, await TestProjects.CreateAsync(client), "Repetida", who);

        var response = await client.PostAsJsonAsync($"/api/v1/tasks/{task}/assignees", new { userId = who });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AssigneesOfAsync(client, task)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Removing_the_primary_promotes_the_next()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var principal = Guid.NewGuid();
        var second = Guid.NewGuid();
        var task = await CreateAsync(client, tenantId, await TestProjects.CreateAsync(client), "Con relevo", principal);
        await client.PostAsJsonAsync($"/api/v1/tasks/{task}/assignees", new { userId = second });

        var deleted = await client.DeleteAsync($"/api/v1/tasks/{task}/assignees/{principal}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var read = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}");
        read.GetProperty("assigneeId").GetGuid().Should().Be(second);
        read.GetProperty("assignees").EnumerateArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task Removing_the_last_assignee_leaves_the_task_unassigned()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var who = Guid.NewGuid();
        var task = await CreateAsync(client, tenantId, await TestProjects.CreateAsync(client), "Se queda sola", who);

        await client.DeleteAsync($"/api/v1/tasks/{task}/assignees/{who}");

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}");
        read.GetProperty("assigneeId").GetGuid().Should().Be(Guid.Empty);
        read.GetProperty("assignees").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task The_assignee_filter_finds_non_primary_assignees()
    {
        // El caso que un filtro que siguiera mirando sólo el campo antiguo perdería en silencio.
        var (client, tenantId) = await AuthenticateAsync();
        var project = await TestProjects.CreateAsync(client);
        var collaborator = Guid.NewGuid();
        var task = await CreateAsync(client, tenantId, project, "La que colabora", Guid.NewGuid());
        await client.PostAsJsonAsync($"/api/v1/tasks/{task}/assignees", new { userId = collaborator });
        await CreateAsync(client, tenantId, project, "Ajena", Guid.NewGuid());

        var page = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/tasks?projectId={project}&assigneeId={collaborator}&pageSize=50");

        page.GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!)
            .Should().ContainSingle().Which.Should().Be("La que colabora");
    }

    [Fact]
    public async Task The_migration_kept_every_primary_as_an_assignee()
    {
        // Sobre los datos que siembra la aplicación al arrancar: si el traspaso hubiera fallado,
        // habría tareas con principal y sin responsables, y no daría ningún error.
        var (client, _) = await AuthenticateAsync();

        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/tasks?pageSize=200&includeSubtasks=true");

        var inconsistent = page.GetProperty("items").EnumerateArray()
            .Where(t => t.GetProperty("assigneeId").GetGuid() != Guid.Empty)
            .Where(t => !t.GetProperty("assignees").EnumerateArray()
                .Select(a => a.GetGuid())
                .Contains(t.GetProperty("assigneeId").GetGuid()))
            .Select(t => t.GetProperty("title").GetString())
            .ToList();

        inconsistent.Should().BeEmpty("todo principal tiene que figurar entre los responsables");
    }
}
