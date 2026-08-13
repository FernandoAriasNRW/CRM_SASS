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

    private async Task<Guid> CrearAsync(HttpClient cliente, Guid tenantId, Guid projectId, string titulo, Guid responsable)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId,
            title = titulo,
            description = "creada por las pruebas de integración",
            assigneeId = responsable,
            estimatedHours = 1m,
            dueDate = "2026-12-01"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<List<Guid>> ResponsablesDeAsync(HttpClient cliente, Guid tarea)
    {
        var leida = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}");
        return leida.GetProperty("assignees").EnumerateArray().Select(x => x.GetGuid()).ToList();
    }

    [Fact]
    public async Task Una_tarea_creada_con_responsable_lo_devuelve_en_la_coleccion()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var quien = Guid.NewGuid();

        var tarea = await CrearAsync(cliente, tenantId, Guid.NewGuid(), "Con responsable", quien);

        (await ResponsablesDeAsync(cliente, tarea)).Should().ContainSingle().Which.Should().Be(quien);
    }

    [Fact]
    public async Task Se_pueden_añadir_varios_responsables()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var principal = Guid.NewGuid();
        var segundo = Guid.NewGuid();
        var tercero = Guid.NewGuid();
        var tarea = await CrearAsync(cliente, tenantId, Guid.NewGuid(), "En equipo", principal);

        foreach (var quien in new[] { segundo, tercero })
        {
            var respuesta = await cliente.PostAsJsonAsync($"/api/v1/tasks/{tarea}/assignees", new { userId = quien });
            respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        (await ResponsablesDeAsync(cliente, tarea)).Should().BeEquivalentTo([principal, segundo, tercero]);

        var leida = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}");
        leida.GetProperty("assigneeId").GetGuid().Should().Be(principal, "añadir gente no cambia el principal");
    }

    [Fact]
    public async Task La_misma_persona_no_se_añade_dos_veces()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var quien = Guid.NewGuid();
        var tarea = await CrearAsync(cliente, tenantId, Guid.NewGuid(), "Repetida", quien);

        var respuesta = await cliente.PostAsJsonAsync($"/api/v1/tasks/{tarea}/assignees", new { userId = quien });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ResponsablesDeAsync(cliente, tarea)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Quitar_al_principal_promueve_al_siguiente()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var principal = Guid.NewGuid();
        var segundo = Guid.NewGuid();
        var tarea = await CrearAsync(cliente, tenantId, Guid.NewGuid(), "Con relevo", principal);
        await cliente.PostAsJsonAsync($"/api/v1/tasks/{tarea}/assignees", new { userId = segundo });

        var borrado = await cliente.DeleteAsync($"/api/v1/tasks/{tarea}/assignees/{principal}");

        borrado.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var leida = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}");
        leida.GetProperty("assigneeId").GetGuid().Should().Be(segundo);
        leida.GetProperty("assignees").EnumerateArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task Quitar_al_ultimo_responsable_deja_la_tarea_sin_asignar()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var quien = Guid.NewGuid();
        var tarea = await CrearAsync(cliente, tenantId, Guid.NewGuid(), "Se queda sola", quien);

        await cliente.DeleteAsync($"/api/v1/tasks/{tarea}/assignees/{quien}");

        var leida = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}");
        leida.GetProperty("assigneeId").GetGuid().Should().Be(Guid.Empty);
        leida.GetProperty("assignees").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task El_filtro_por_responsable_encuentra_a_quien_no_es_el_principal()
    {
        // El caso que un filtro que siguiera mirando sólo el campo antiguo perdería en silencio.
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var colaborador = Guid.NewGuid();
        var tarea = await CrearAsync(cliente, tenantId, proyecto, "La que colabora", Guid.NewGuid());
        await cliente.PostAsJsonAsync($"/api/v1/tasks/{tarea}/assignees", new { userId = colaborador });
        await CrearAsync(cliente, tenantId, proyecto, "Ajena", Guid.NewGuid());

        var pagina = await cliente.GetFromJsonAsync<JsonElement>(
            $"/api/v1/tasks?projectId={proyecto}&assigneeId={colaborador}&pageSize=50");

        pagina.GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!)
            .Should().ContainSingle().Which.Should().Be("La que colabora");
    }

    [Fact]
    public async Task El_traspaso_de_la_migracion_dejo_a_todo_principal_como_responsable()
    {
        // Sobre los datos que siembra la aplicación al arrancar: si el traspaso hubiera fallado,
        // habría tareas con principal y sin responsables, y no daría ningún error.
        var (cliente, _) = await AutenticarAsync();

        var pagina = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tasks?pageSize=200&includeSubtasks=true");

        var incoherentes = pagina.GetProperty("items").EnumerateArray()
            .Where(t => t.GetProperty("assigneeId").GetGuid() != Guid.Empty)
            .Where(t => !t.GetProperty("assignees").EnumerateArray()
                .Select(a => a.GetGuid())
                .Contains(t.GetProperty("assigneeId").GetGuid()))
            .Select(t => t.GetProperty("title").GetString())
            .ToList();

        incoherentes.Should().BeEmpty("todo principal tiene que figurar entre los responsables");
    }
}
