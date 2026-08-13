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

    private async Task<(HttpClient cliente, Guid tenantId)> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var cuerpo = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var relleno = cuerpo.PadRight(cuerpo.Length + (4 - cuerpo.Length % 4) % 4, '=');
        var tenantId = Guid.Parse(JsonDocument.Parse(Convert.FromBase64String(relleno))
            .RootElement.GetProperty("tenantId").GetString()!);

        return (cliente, tenantId);
    }

    private static object CuerpoDeTarea(Guid tenantId, Guid projectId, string titulo, Guid? padre) => new
    {
        tenantId,
        createdById = Guid.NewGuid(),
        projectId,
        title = titulo,
        description = "creada por las pruebas de integración",
        assigneeId = Guid.NewGuid(),
        estimatedHours = 1m,
        dueDate = "2026-12-01",
        parentTaskId = padre
    };

    private async Task<Guid> CrearAsync(HttpClient cliente, Guid tenantId, Guid projectId, string titulo, Guid? padre = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks", CuerpoDeTarea(tenantId, projectId, titulo, padre));
        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> LeerAsync(HttpClient cliente, Guid id)
        => await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{id}");

    [Fact]
    public async Task Una_subtarea_guarda_su_padre_y_lo_devuelve()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var projectId = Guid.NewGuid();
        var padre = await CrearAsync(cliente, tenantId, projectId, "Padre");

        var hija = await CrearAsync(cliente, tenantId, projectId, "Hija", padre);

        (await LeerAsync(cliente, hija)).GetProperty("parentTaskId").GetGuid().Should().Be(padre);
    }

    [Fact]
    public async Task Las_listas_devuelven_solo_tareas_de_primer_nivel()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var projectId = Guid.NewGuid();
        var padre = await CrearAsync(cliente, tenantId, projectId, "Padre visible");
        await CrearAsync(cliente, tenantId, projectId, "Hija escondida", padre);

        var pagina = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks?projectId={projectId}&pageSize=200");
        var titulos = pagina.GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!).ToList();

        titulos.Should().ContainSingle().Which.Should().Be("Padre visible");
        pagina.GetProperty("totalCount").GetInt32().Should().Be(1,
            "el total de la paginación cuenta tareas, no subtareas");
    }

    [Fact]
    public async Task Las_subtareas_se_piden_por_su_padre()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var projectId = Guid.NewGuid();
        var padre = await CrearAsync(cliente, tenantId, projectId, "Padre");
        await CrearAsync(cliente, tenantId, projectId, "Hija 1", padre);
        await CrearAsync(cliente, tenantId, projectId, "Hija 2", padre);

        var pagina = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{padre}/subtasks");

        pagina.GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!)
            .Should().BeEquivalentTo(["Hija 1", "Hija 2"]);
    }

    [Fact]
    public async Task El_progreso_del_padre_cuenta_las_subtareas_completadas()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var projectId = Guid.NewGuid();
        var padre = await CrearAsync(cliente, tenantId, projectId, "Padre con progreso");
        var hija1 = await CrearAsync(cliente, tenantId, projectId, "Hija 1", padre);
        await CrearAsync(cliente, tenantId, projectId, "Hija 2", padre);
        await CrearAsync(cliente, tenantId, projectId, "Hija 3", padre);

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{hija1}", new { status = "Done" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var leido = await LeerAsync(cliente, padre);

        leido.GetProperty("subtaskCount").GetInt32().Should().Be(3);
        leido.GetProperty("completedSubtaskCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Una_subtarea_no_puede_tener_subtareas()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var projectId = Guid.NewGuid();
        var padre = await CrearAsync(cliente, tenantId, projectId, "Padre");
        var hija = await CrearAsync(cliente, tenantId, projectId, "Hija", padre);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks",
            CuerpoDeTarea(tenantId, projectId, "Nieta", hija));

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("un solo nivel");
    }

    [Fact]
    public async Task Una_tarea_se_puede_colgar_y_desligar_despues()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var projectId = Guid.NewGuid();
        var padre = await CrearAsync(cliente, tenantId, projectId, "Padre");
        var suelta = await CrearAsync(cliente, tenantId, projectId, "Suelta");

        var colgar = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{suelta}/parent", new { parentTaskId = padre });
        colgar.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LeerAsync(cliente, suelta)).GetProperty("parentTaskId").GetGuid().Should().Be(padre);

        var desligar = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{suelta}/parent", new { parentTaskId = (Guid?)null });
        desligar.StatusCode.Should().Be(HttpStatusCode.OK);

        var despues = await LeerAsync(cliente, suelta);
        despues.TryGetProperty("parentTaskId", out var valor).Should().BeTrue();
        valor.ValueKind.Should().Be(JsonValueKind.Null, "desligar deja la tarea de primer nivel");
    }

    [Fact]
    public async Task Una_tarea_con_subtareas_no_se_puede_convertir_en_subtarea()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var projectId = Guid.NewGuid();
        var conHijas = await CrearAsync(cliente, tenantId, projectId, "Con hijas");
        await CrearAsync(cliente, tenantId, projectId, "Hija", conHijas);
        var otra = await CrearAsync(cliente, tenantId, projectId, "Otra");

        var respuesta = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{conHijas}/parent", new { parentTaskId = otra });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("no puede convertirse en subtarea");
    }
}
