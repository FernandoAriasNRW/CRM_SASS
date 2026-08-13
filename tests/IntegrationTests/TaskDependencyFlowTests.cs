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

    private async Task<Guid> CrearAsync(HttpClient cliente, Guid tenantId, Guid projectId, string titulo)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            tenantId,
            createdById = Guid.NewGuid(),
            projectId,
            title = titulo,
            description = "creada por las pruebas de integración",
            assigneeId = Guid.NewGuid(),
            estimatedHours = 1m,
            dueDate = "2026-12-01"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> BloquearAsync(HttpClient cliente, Guid tarea, Guid bloqueante)
        => cliente.PostAsJsonAsync($"/api/v1/tasks/{tarea}/dependencies", new { dependsOnTaskId = bloqueante });

    [Fact]
    public async Task Una_tarea_puede_quedar_bloqueada_por_otra_y_se_ve_en_las_dos_direcciones()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var tarea = await CrearAsync(cliente, tenantId, proyecto, "La que espera");
        var bloqueante = await CrearAsync(cliente, tenantId, proyecto, "La que bloquea");

        (await BloquearAsync(cliente, tarea, bloqueante)).StatusCode.Should().Be(HttpStatusCode.OK);

        var deLaQueEspera = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}/dependencies");
        deLaQueEspera.GetProperty("bloqueadaPor").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!)
            .Should().ContainSingle().Which.Should().Be("La que bloquea");

        var deLaQueBloquea = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{bloqueante}/dependencies");
        deLaQueBloquea.GetProperty("bloqueaA").EnumerateArray()
            .Select(t => t.GetProperty("title").GetString()!)
            .Should().ContainSingle().Which.Should().Be("La que espera");
    }

    [Fact]
    public async Task Los_recuentos_de_bloqueo_llegan_en_el_listado()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var tarea = await CrearAsync(cliente, tenantId, proyecto, "Bloqueada");
        var uno = await CrearAsync(cliente, tenantId, proyecto, "Bloqueante 1");
        var dos = await CrearAsync(cliente, tenantId, proyecto, "Bloqueante 2");

        await BloquearAsync(cliente, tarea, uno);
        await BloquearAsync(cliente, tarea, dos);

        var leida = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}");
        leida.GetProperty("blockedByCount").GetInt32().Should().Be(2);
        leida.GetProperty("blocksCount").GetInt32().Should().Be(0);

        var bloqueante = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{uno}");
        bloqueante.GetProperty("blocksCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Una_tarea_no_puede_bloquearse_a_si_misma()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var tarea = await CrearAsync(cliente, tenantId, proyecto, "Sola");

        var respuesta = await BloquearAsync(cliente, tarea, tarea);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("a sí misma");
    }

    [Fact]
    public async Task El_ciclo_directo_se_rechaza()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var a = await CrearAsync(cliente, tenantId, proyecto, "A");
        var b = await CrearAsync(cliente, tenantId, proyecto, "B");

        (await BloquearAsync(cliente, a, b)).StatusCode.Should().Be(HttpStatusCode.OK);

        var respuesta = await BloquearAsync(cliente, b, a);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("ciclo");
    }

    [Fact]
    public async Task El_ciclo_largo_se_rechaza()
    {
        // A←B←C, y cerrar C←A. Es el caso que sólo se detecta recorriendo el grafo, no mirando
        // la arista que se añade.
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var a = await CrearAsync(cliente, tenantId, proyecto, "A");
        var b = await CrearAsync(cliente, tenantId, proyecto, "B");
        var c = await CrearAsync(cliente, tenantId, proyecto, "C");

        (await BloquearAsync(cliente, a, b)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await BloquearAsync(cliente, b, c)).StatusCode.Should().Be(HttpStatusCode.OK);

        var respuesta = await BloquearAsync(cliente, c, a);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("ciclo");
    }

    [Fact]
    public async Task Una_cadena_larga_legitima_se_acepta()
    {
        // Contrapeso del test anterior: comprobar que se rechazan los ciclos no sirve de nada si
        // de paso se rechazan las cadenas válidas.
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var a = await CrearAsync(cliente, tenantId, proyecto, "A");
        var b = await CrearAsync(cliente, tenantId, proyecto, "B");
        var c = await CrearAsync(cliente, tenantId, proyecto, "C");

        (await BloquearAsync(cliente, a, b)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await BloquearAsync(cliente, b, c)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{a}"))
            .GetProperty("blockedByCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task La_misma_dependencia_no_se_registra_dos_veces()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var tarea = await CrearAsync(cliente, tenantId, proyecto, "Repetida");
        var bloqueante = await CrearAsync(cliente, tenantId, proyecto, "Bloqueante");

        (await BloquearAsync(cliente, tarea, bloqueante)).StatusCode.Should().Be(HttpStatusCode.OK);
        var segunda = await BloquearAsync(cliente, tarea, bloqueante);

        segunda.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}"))
            .GetProperty("blockedByCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Las_dependencias_solo_se_establecen_dentro_del_mismo_proyecto()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var tarea = await CrearAsync(cliente, tenantId, Guid.NewGuid(), "De un proyecto");
        var ajena = await CrearAsync(cliente, tenantId, Guid.NewGuid(), "De otro proyecto");

        var respuesta = await BloquearAsync(cliente, tarea, ajena);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("mismo proyecto");
    }

    [Fact]
    public async Task Una_dependencia_se_puede_quitar()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var tarea = await CrearAsync(cliente, tenantId, proyecto, "Se desbloquea");
        var bloqueante = await CrearAsync(cliente, tenantId, proyecto, "Deja de bloquear");
        await BloquearAsync(cliente, tarea, bloqueante);

        var borrado = await cliente.DeleteAsync($"/api/v1/tasks/{tarea}/dependencies/{bloqueante}");

        borrado.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{tarea}"))
            .GetProperty("blockedByCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Quitar_una_dependencia_que_no_existe_se_rechaza()
    {
        var (cliente, tenantId) = await AutenticarAsync();
        var proyecto = Guid.NewGuid();
        var tarea = await CrearAsync(cliente, tenantId, proyecto, "Sin bloqueos");
        var otra = await CrearAsync(cliente, tenantId, proyecto, "Otra");

        var borrado = await cliente.DeleteAsync($"/api/v1/tasks/{tarea}/dependencies/{otra}");

        borrado.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
