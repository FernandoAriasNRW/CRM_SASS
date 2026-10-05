using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Dependencias entre tareas de punta a punta contra la API real.
///
/// Lo que sólo se ve aquí:
///
/// 1. Que la **unicidad** la garantice la base y no sólo la comprobación previa del handler.
/// 2. Que los **recuentos de bloqueo** de cada tarjeta salgan de la consulta, no de pedir las
///    dependencias tarea por tarea.
/// 3. Que el detector de ciclos reciba de verdad las aristas guardadas: la lógica está probada
///    aparte y sin base de datos, pero que el handler le pase el grafo correcto sólo se
///    comprueba con datos reales.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TaskDependencyFlowTests(CrmApiFactory factory)
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

    private async Task<Guid> CreateAsync(HttpClient client, Guid tenantId, Guid projectId, string title)
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
            dueDate = "2026-12-01"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> BlockAsync(HttpClient client, Guid task, Guid bloqueante)
        => client.PostAsJsonAsync($"/api/v1/tasks/{task}/dependencies", new { dependsOnTaskId = bloqueante });

    [Fact]
    public async Task A_task_can_be_blocked_by_another_and_it_shows_both_ways()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var project = Guid.NewGuid();
        var task = await CreateAsync(client, tenantId, project, "La que espera");
        var bloqueante = await CreateAsync(client, tenantId, project, "La que bloquea");

        (await BlockAsync(client, task, bloqueante)).StatusCode.Should().Be(HttpStatusCode.OK);

        var ofWaiting = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}/dependencies");
        ofWaiting.GetProperty("bloqueadaPor").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!)
            .Should().ContainSingle().Which.Should().Be("La que bloquea");

        var ofBlocker = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{bloqueante}/dependencies");
        ofBlocker.GetProperty("bloqueaA").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!)
            .Should().ContainSingle().Which.Should().Be("La que espera");
    }

    [Fact]
    public async Task Blocking_counts_come_in_the_listing()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var project = Guid.NewGuid();
        var task = await CreateAsync(client, tenantId, project, "Bloqueada");
        var one = await CreateAsync(client, tenantId, project, "Bloqueante 1");
        var two = await CreateAsync(client, tenantId, project, "Bloqueante 2");

        await BlockAsync(client, task, one);
        await BlockAsync(client, task, two);

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}");
        read.GetProperty("blockedByCount").GetInt32().Should().Be(2);
        read.GetProperty("blocksCount").GetInt32().Should().Be(0);

        var bloqueante = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{one}");
        bloqueante.GetProperty("blocksCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task A_task_cannot_block_itself()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var project = Guid.NewGuid();
        var task = await CreateAsync(client, tenantId, project, "Sola");

        var response = await BlockAsync(client, task, task);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("a sí misma");
    }

    [Fact]
    public async Task A_direct_cycle_is_rejected()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var project = Guid.NewGuid();
        var a = await CreateAsync(client, tenantId, project, "A");
        var b = await CreateAsync(client, tenantId, project, "B");

        (await BlockAsync(client, a, b)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await BlockAsync(client, b, a);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ciclo");
    }

    [Fact]
    public async Task A_long_cycle_is_rejected()
    {
        // A←B←C, y cerrar C←A. Es el caso que sólo se detecta recorriendo el grafo, no mirando
        // la arista que se añade.
        var (client, tenantId) = await AuthenticateAsync();
        var project = Guid.NewGuid();
        var a = await CreateAsync(client, tenantId, project, "A");
        var b = await CreateAsync(client, tenantId, project, "B");
        var c = await CreateAsync(client, tenantId, project, "C");

        (await BlockAsync(client, a, b)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await BlockAsync(client, b, c)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await BlockAsync(client, c, a);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("ciclo");
    }

    [Fact]
    public async Task A_long_valid_chain_is_accepted()
    {
        // Contrapeso del test anterior: comprobar que se rechazan los ciclos no sirve de nada si
        // de paso se rechazan las cadenas válidas.
        var (client, tenantId) = await AuthenticateAsync();
        var project = Guid.NewGuid();
        var a = await CreateAsync(client, tenantId, project, "A");
        var b = await CreateAsync(client, tenantId, project, "B");
        var c = await CreateAsync(client, tenantId, project, "C");

        (await BlockAsync(client, a, b)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await BlockAsync(client, b, c)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{a}"))
            .GetProperty("blockedByCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task The_same_dependency_is_not_registered_twice()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var project = Guid.NewGuid();
        var task = await CreateAsync(client, tenantId, project, "Repetida");
        var bloqueante = await CreateAsync(client, tenantId, project, "Bloqueante");

        (await BlockAsync(client, task, bloqueante)).StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await BlockAsync(client, task, bloqueante);

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}"))
            .GetProperty("blockedByCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Dependencies_only_exist_within_one_project()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var task = await CreateAsync(client, tenantId, Guid.NewGuid(), "De un proyecto");
        var foreign = await CreateAsync(client, tenantId, Guid.NewGuid(), "De otro proyecto");

        var response = await BlockAsync(client, task, foreign);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("mismo proyecto");
    }

    [Fact]
    public async Task A_dependency_can_be_removed()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var project = Guid.NewGuid();
        var task = await CreateAsync(client, tenantId, project, "Se desbloquea");
        var bloqueante = await CreateAsync(client, tenantId, project, "Deja de bloquear");
        await BlockAsync(client, task, bloqueante);

        var deleted = await client.DeleteAsync($"/api/v1/tasks/{task}/dependencies/{bloqueante}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}"))
            .GetProperty("blockedByCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Removing_a_missing_dependency_is_rejected()
    {
        var (client, tenantId) = await AuthenticateAsync();
        var project = Guid.NewGuid();
        var task = await CreateAsync(client, tenantId, project, "Sin bloqueos");
        var other = await CreateAsync(client, tenantId, project, "Otra");

        var deleted = await client.DeleteAsync($"/api/v1/tasks/{task}/dependencies/{other}");

        deleted.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
