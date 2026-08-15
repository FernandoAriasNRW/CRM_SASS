using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El motor de automatizaciones, de punta a punta contra la API real.
///
/// Aquí está la prueba que de verdad importa del 4D: **se configura una regla, se mueve una
/// tarea y se comprueba que la tarea cambió sola**. Recorre la cadena entera —evento de dominio,
/// puente del host, motor, acción de vuelta sobre WorkItems— y ninguna prueba unitaria puede
/// cubrirla, porque lo que se está probando es precisamente que las piezas están conectadas.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AutomationsFlowTests(CrmApiFactory factory)
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

        return (cliente, TenantDelToken(token));
    }

    private static Guid TenantDelToken(string token)
    {
        var cuerpo = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var relleno = cuerpo.PadRight(cuerpo.Length + (4 - cuerpo.Length % 4) % 4, '=');
        var json = JsonDocument.Parse(Convert.FromBase64String(relleno));

        return Guid.Parse(json.RootElement.GetProperty("tenantId").GetString()!);
    }

    private async Task<Guid> CrearTareaAsync(HttpClient cliente, Guid tenantId, string titulo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId = Guid.NewGuid(),
            title = titulo,
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 2m,
            dueDate = "2026-12-01",
            priority = "Normal"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static object ReglaQueBajaLaPrioridadAlCerrar(string nombre) => new
    {
        nombre,
        disparador = "TareaCambiaDeEstado",
        condiciones = new[] { new { campo = "Estado", operador = "Igual", valor = "Done" } },
        acciones = new[] { new { tipo = "CambiarPrioridad", valor = "Low" } },
    };

    /// <summary>
    /// Borra las reglas que haya antes de empezar.
    ///
    /// Las pruebas de esta colección comparten un MySQL, y una automatización activa que dejó
    /// otra prueba **se ejecuta igual**: eso es lo que hace el motor. Sin limpiar, comprobar que
    /// «esta regla no se ejecutó» falla porque se ejecutó otra, y el fallo depende del orden.
    /// </summary>
    private static async Task LimpiarReglasAsync(HttpClient cliente)
    {
        var reglas = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/automations");

        foreach (var regla in reglas.EnumerateArray())
            await cliente.DeleteAsync($"/api/v1/automations/{regla.GetProperty("id").GetGuid()}");
    }

    private async Task<Guid> CrearReglaAsync(HttpClient cliente, object regla)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/automations", regla);
        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReglaAsync(HttpClient cliente, Guid id)
    {
        var reglas = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/automations");
        return reglas.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Una_regla_se_ejecuta_sola_cuando_se_cumple_su_disparador()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        await LimpiarReglasAsync(cliente);
        var reglaId = await CrearReglaAsync(cliente, ReglaQueBajaLaPrioridadAlCerrar($"Bajar al cerrar {Guid.NewGuid()}"));
        var tareaId = await CrearTareaAsync(cliente, tenantId, "Tarea que se cerrará");

        await cliente.PatchAsJsonAsync($"/api/v1/tasks/{tareaId}", new { status = "Done" });

        var tarea = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tareaId}");
        tarea.GetProperty("priority").GetString().Should().Be("Low",
            "la automatización tenía que haber bajado la prioridad al pasar la tarea a Done");

        var regla = await ReglaAsync(cliente, reglaId);
        regla.GetProperty("vecesEjecutada").GetInt32().Should().Be(1);
        regla.GetProperty("ultimaEjecucionUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    /// <summary>
    /// Es el reverso de la prueba anterior y hace falta: sin ella, una regla que se ejecutara
    /// siempre —ignorando sus condiciones— pasaría la primera igual de bien.
    /// </summary>
    [Fact]
    public async Task Una_regla_no_se_ejecuta_si_sus_condiciones_no_se_cumplen()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        await LimpiarReglasAsync(cliente);
        var reglaId = await CrearReglaAsync(cliente, ReglaQueBajaLaPrioridadAlCerrar($"Bajar al cerrar {Guid.NewGuid()}"));
        var tareaId = await CrearTareaAsync(cliente, tenantId, "Tarea que sólo avanza");

        await cliente.PatchAsJsonAsync($"/api/v1/tasks/{tareaId}", new { status = "In Progress" });

        var tarea = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tareaId}");
        tarea.GetProperty("priority").GetString().Should().Be("Normal");

        (await ReglaAsync(cliente, reglaId)).GetProperty("vecesEjecutada").GetInt32().Should().Be(0);
    }

    /// <summary>
    /// Desactivar es la operación que se hace con prisa, cuando una automatización está haciendo
    /// daño. Si la regla siguiera ejecutándose, el botón sería decorativo.
    /// </summary>
    [Fact]
    public async Task Una_regla_desactivada_no_se_ejecuta()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        await LimpiarReglasAsync(cliente);
        var reglaId = await CrearReglaAsync(cliente, ReglaQueBajaLaPrioridadAlCerrar($"Desactivada {Guid.NewGuid()}"));

        var apagar = await cliente.PutAsJsonAsync($"/api/v1/automations/{reglaId}/active", new { activa = false });
        apagar.StatusCode.Should().Be(HttpStatusCode.OK);

        var tareaId = await CrearTareaAsync(cliente, tenantId, "Tarea con la regla apagada");
        await cliente.PatchAsJsonAsync($"/api/v1/tasks/{tareaId}", new { status = "Done" });

        var tarea = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tareaId}");
        tarea.GetProperty("priority").GetString().Should().Be("Normal");
    }

    /// <summary>
    /// Lo que garantiza que las automatizaciones no se encadenen: la acción de una regla emite su
    /// propio evento, y si ese evento disparara otras reglas, dos reglas que se deshacen la una a
    /// la otra se llamarían para siempre.
    /// </summary>
    [Fact]
    public async Task Las_acciones_de_una_regla_no_disparan_otras_reglas()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        await LimpiarReglasAsync(cliente);

        // La primera pasa la tarea a Done; la segunda reaccionaría a ese Done bajando la
        // prioridad. Si las cadenas existieran, la prioridad acabaría en Low.
        await CrearReglaAsync(cliente, new
        {
            nombre = $"Cerrar al revisar {Guid.NewGuid()}",
            disparador = "TareaCambiaDeEstado",
            condiciones = new[] { new { campo = "Estado", operador = "Igual", valor = "In Review" } },
            acciones = new[] { new { tipo = "CambiarEstado", valor = "Done" } },
        });

        await CrearReglaAsync(cliente, new
        {
            nombre = $"Bajar al cerrar {Guid.NewGuid()}",
            disparador = "TareaCambiaDeEstado",
            condiciones = new[] { new { campo = "Estado", operador = "Igual", valor = "Done" } },
            acciones = new[] { new { tipo = "CambiarPrioridad", valor = "Low" } },
        });

        var tareaId = await CrearTareaAsync(cliente, tenantId, "Tarea que pasa por revisión");
        await cliente.PatchAsJsonAsync($"/api/v1/tasks/{tareaId}", new { status = "In Review" });

        var tarea = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tareaId}");
        tarea.GetProperty("status").GetString().Should().Be("Done", "la primera regla sí se ejecutó");
        tarea.GetProperty("priority").GetString().Should().Be("Normal",
            "la segunda no, porque las acciones de una automatización no disparan otras");
    }

    [Fact]
    public async Task Una_regla_sin_acciones_se_rechaza()
    {
        var (cliente, _) = await AutenticarAsync();
        await LimpiarReglasAsync(cliente);

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/automations", new
        {
            nombre = $"Sin acciones {Guid.NewGuid()}",
            disparador = "TareaCambiaDeEstado",
            condiciones = Array.Empty<object>(),
            acciones = Array.Empty<object>(),
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dos_reglas_no_pueden_llamarse_igual()
    {
        var (cliente, _) = await AutenticarAsync();
        await LimpiarReglasAsync(cliente);
        var nombre = $"Repetida {Guid.NewGuid()}";

        await CrearReglaAsync(cliente, ReglaQueBajaLaPrioridadAlCerrar(nombre));
        var segunda = await cliente.PostAsJsonAsync("/api/v1/automations", ReglaQueBajaLaPrioridadAlCerrar(nombre));

        segunda.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task El_vocabulario_lo_sirve_el_servidor()
    {
        var (cliente, _) = await AutenticarAsync();
        await LimpiarReglasAsync(cliente);

        var vocabulario = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/automations/vocabulario");

        // La interfaz construye el formulario con esto. Repetir la lista en el cliente la dejaría
        // desincronizada el día que se añada un disparador.
        vocabulario.GetProperty("disparadores").GetArrayLength().Should().BeGreaterThan(0);
        vocabulario.GetProperty("operadores").GetArrayLength().Should().BeGreaterThan(0);
        vocabulario.GetProperty("acciones").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Una_regla_se_puede_borrar()
    {
        var (cliente, _) = await AutenticarAsync();
        await LimpiarReglasAsync(cliente);
        var reglaId = await CrearReglaAsync(cliente, ReglaQueBajaLaPrioridadAlCerrar($"Para borrar {Guid.NewGuid()}"));

        var borrado = await cliente.DeleteAsync($"/api/v1/automations/{reglaId}");
        borrado.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reglas = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/automations");
        reglas.EnumerateArray().Should().NotContain(r => r.GetProperty("id").GetGuid() == reglaId);
    }

    [Fact]
    public async Task Una_regla_que_no_existe_da_404()
    {
        var (cliente, _) = await AutenticarAsync();
        await LimpiarReglasAsync(cliente);

        var respuesta = await cliente.DeleteAsync($"/api/v1/automations/{Guid.NewGuid()}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
