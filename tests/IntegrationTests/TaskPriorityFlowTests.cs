using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// La prioridad de una tarea, de punta a punta contra la API real.
///
/// Estas pruebas van contra MySQL de verdad porque hay dos cosas que sólo fallan ahí:
///
/// 1. El orden por prioridad se traduce a un CASE en SQL. Un orden que se calculara en
///    memoria pasaría cualquier prueba unitaria y devolvería la página equivocada en cuanto
///    hubiera más tareas que tamaño de página.
/// 2. Las columnas nuevas llevan valor por defecto en la base, y MySQL no admite DEFAULT en
///    TEXT: si alguien le quita la longitud al mapeo, la migración deja de aplicarse y la
///    API no arranca. Aquí se ve.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TaskPriorityFlowTests(CrmApiFactory factory)
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

        // El tenant se saca del propio token: las tareas se crean y se consultan en el mismo,
        // que es lo que el filtro global exige para encontrarlas después.
        return (client, TenantFromToken(token));
    }

    /// <summary>
    /// Lee el tenant del cuerpo del JWT sin librerías: al proyecto de pruebas no le hace
    /// falta una dependencia de validación para leer un claim.
    /// </summary>
    private static Guid TenantFromToken(string token)
    {
        var body = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var payload = body.PadRight(body.Length + (4 - body.Length % 4) % 4, '=');
        var json = JsonDocument.Parse(Convert.FromBase64String(payload));

        return Guid.Parse(json.RootElement.GetProperty("tenantId").GetString()!);
    }

    private static object TaskBody(Guid tenantId, Guid projectId, string title, string? priority) => new
    {
        tenantId,
        createdById = Guid.NewGuid(),
        projectId,
        title = title,
        description = "creada por las pruebas de integración",
        assigneeId = Guid.NewGuid(),
        estimatedHours = 2m,
        dueDate = "2026-12-01",
        priority = priority
    };

    private async Task<JsonElement> CreateAsync(HttpClient client, Guid tenantId, string title, string? priority)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", TaskBody(tenantId, await TestProjects.CreateAsync(client), title, priority));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task A_task_created_with_a_priority_keeps_it_on_read()
    {
        var (client, tenantId) = await AuthenticateAsync();

        var created = await CreateAsync(client, tenantId, "Urgente de verdad", "Urgent");
        var id = created.GetProperty("id").GetGuid();

        var retrieved = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{id}");

        retrieved.GetProperty("priority").GetString().Should().Be("Urgent");
    }

    [Fact]
    public async Task A_task_without_priority_is_saved_as_Normal()
    {
        var (client, tenantId) = await AuthenticateAsync();

        var created = await CreateAsync(client, tenantId, "Sin prioridad explícita", null);
        var id = created.GetProperty("id").GetGuid();

        var retrieved = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{id}");

        retrieved.GetProperty("priority").GetString().Should().Be("Normal",
            "una prioridad vacía no la pintaría ninguna vista ni la encontraría ningún filtro");
    }

    [Fact]
    public async Task Priority_can_change_and_the_change_persists()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var id = (await CreateAsync(client, tenantId, "Para repriorizar", "Low")).GetProperty("id").GetGuid();

        var patch = await client.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { priority = "Urgent" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrieved = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{id}");
        retrieved.GetProperty("priority").GetString().Should().Be("Urgent");
    }

    [Fact]
    public async Task An_unknown_priority_is_rejected_and_never_stored()
    {
        var (client, tenantId) = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/tasks",
            TaskBody(tenantId, await TestProjects.CreateAsync(client), "Prioridad inventada", "Altísima"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Tasks_can_be_filtered_by_priority()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var mark = $"filtro-{Guid.NewGuid():N}";
        await CreateAsync(client, tenantId, $"{mark} urgente", "Urgent");
        await CreateAsync(client, tenantId, $"{mark} baja", "Low");

        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/tasks?priority=Urgent&pageSize=200");

        var titles = page.GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!)
            .Where(t => t.StartsWith(mark))
            .ToList();

        titles.Should().ContainSingle().Which.Should().Be($"{mark} urgente");
    }

    /// <summary>
    /// El orden es el de negocio, y se calcula en la base de datos.
    ///
    /// Ordenar por la columna de texto daría High, Low, Normal, Urgent —alfabético—, que es
    /// justo lo que no se quiere. Se comprueba con las cuatro prioridades creadas en orden
    /// inverso, para que un orden de inserción no pueda dar el resultado por casualidad.
    /// </summary>
    [Fact]
    public async Task Sorting_by_priority_goes_from_most_to_least_urgent()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var mark = $"orden-{Guid.NewGuid():N}";

        foreach (var priority in new[] { "Low", "Normal", "High", "Urgent" })
            await CreateAsync(client, tenantId, $"{mark} {priority}", priority);

        var page = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/tasks?sortColumn=priority&sortDirection=asc&pageSize=200");

        var prioritiesInOrder = page.GetProperty("items").EnumerateArray()
            .Where(t => t.GetProperty("title").GetString()!.StartsWith(mark))
            .Select(t => t.GetProperty("priority").GetString()!)
            .ToList();

        prioritiesInOrder.Should().Equal("Urgent", "High", "Normal", "Low");
    }
}
