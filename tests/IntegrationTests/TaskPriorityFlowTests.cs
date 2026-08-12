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

    private async Task<(HttpClient cliente, Guid tenantId)> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var cuerpo = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = cuerpo.GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // El tenant se saca del propio token: las tareas se crean y se consultan en el mismo,
        // que es lo que el filtro global exige para encontrarlas después.
        return (cliente, TenantDelToken(token));
    }

    /// <summary>
    /// Lee el tenant del cuerpo del JWT sin librerías: al proyecto de pruebas no le hace
    /// falta una dependencia de validación para leer un claim.
    /// </summary>
    private static Guid TenantDelToken(string token)
    {
        var cuerpo = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var relleno = cuerpo.PadRight(cuerpo.Length + (4 - cuerpo.Length % 4) % 4, '=');
        var json = JsonDocument.Parse(Convert.FromBase64String(relleno));

        return Guid.Parse(json.RootElement.GetProperty("tenantId").GetString()!);
    }

    private static object CuerpoDeTarea(Guid tenantId, string titulo, string? prioridad) => new
    {
        tenantId,
        createdById = Guid.NewGuid(),
        projectId = Guid.NewGuid(),
        title = titulo,
        description = "creada por las pruebas de integración",
        assigneeId = Guid.NewGuid(),
        estimatedHours = 2m,
        dueDate = "2026-12-01",
        priority = prioridad
    };

    private async Task<JsonElement> CrearAsync(HttpClient cliente, Guid tenantId, string titulo, string? prioridad)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks", CuerpoDeTarea(tenantId, titulo, prioridad));
        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Una_tarea_creada_con_prioridad_la_conserva_al_recuperarla()
    {
        var (cliente, tenantId) = await AutenticarAsync();

        var creada = await CrearAsync(cliente, tenantId, "Urgente de verdad", "Urgent");
        var id = creada.GetProperty("id").GetGuid();

        var recuperada = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{id}");

        recuperada.GetProperty("priority").GetString().Should().Be("Urgent");
    }

    [Fact]
    public async Task Una_tarea_sin_prioridad_se_guarda_como_Normal()
    {
        var (cliente, tenantId) = await AutenticarAsync();

        var creada = await CrearAsync(cliente, tenantId, "Sin prioridad explícita", null);
        var id = creada.GetProperty("id").GetGuid();

        var recuperada = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{id}");

        recuperada.GetProperty("priority").GetString().Should().Be("Normal",
            "una prioridad vacía no la pintaría ninguna vista ni la encontraría ningún filtro");
    }

    [Fact]
    public async Task La_prioridad_se_puede_cambiar_y_el_cambio_persiste()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var id = (await CrearAsync(cliente, tenantId, "Para repriorizar", "Low")).GetProperty("id").GetGuid();

        var patch = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{id}", new { priority = "Urgent" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var recuperada = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{id}");
        recuperada.GetProperty("priority").GetString().Should().Be("Urgent");
    }

    [Fact]
    public async Task Una_prioridad_inexistente_se_rechaza_y_no_llega_a_la_base()
    {
        var (cliente, tenantId) = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks",
            CuerpoDeTarea(tenantId, "Prioridad inventada", "Altísima"));

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Se_puede_filtrar_por_prioridad()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var marca = $"filtro-{Guid.NewGuid():N}";
        await CrearAsync(cliente, tenantId, $"{marca} urgente", "Urgent");
        await CrearAsync(cliente, tenantId, $"{marca} baja", "Low");

        var pagina = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tasks?priority=Urgent&pageSize=200");

        var titulos = pagina.GetProperty("items").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!)
            .Where(t => t.StartsWith(marca))
            .ToList();

        titulos.Should().ContainSingle().Which.Should().Be($"{marca} urgente");
    }

    /// <summary>
    /// El orden es el de negocio, y se calcula en la base de datos.
    ///
    /// Ordenar por la columna de texto daría High, Low, Normal, Urgent —alfabético—, que es
    /// justo lo que no se quiere. Se comprueba con las cuatro prioridades creadas en orden
    /// inverso, para que un orden de inserción no pueda dar el resultado por casualidad.
    /// </summary>
    [Fact]
    public async Task Ordenar_por_prioridad_devuelve_de_mas_urgente_a_menos()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var marca = $"orden-{Guid.NewGuid():N}";

        foreach (var prioridad in new[] { "Low", "Normal", "High", "Urgent" })
            await CrearAsync(cliente, tenantId, $"{marca} {prioridad}", prioridad);

        var pagina = await cliente.GetFromJsonAsync<JsonElement>(
            "/api/v1/tasks?sortColumn=priority&sortDirection=asc&pageSize=200");

        var prioridadesEnOrden = pagina.GetProperty("items").EnumerateArray()
            .Where(t => t.GetProperty("title").GetString()!.StartsWith(marca))
            .Select(t => t.GetProperty("priority").GetString()!)
            .ToList();

        prioridadesEnOrden.Should().Equal("Urgent", "High", "Normal", "Low");
    }
}
