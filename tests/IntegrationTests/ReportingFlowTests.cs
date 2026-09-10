using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Reporting, de punta a punta contra la API real.
///
/// Existen porque **no existían**. Las 97 pruebas de integración que había sólo tocaban seis
/// familias de rutas —auth, tasks, projects, comments, automations y custom-fields—; nadie había
/// llamado nunca a las ocho de Reporting. Que salieran cubiertas en el informe era un espejismo:
/// en una API mínima la línea `group.MapGet(...)` se ejecuta al registrar la ruta, o sea en el
/// arranque, así que basta con levantar el host para que el registro cuente como cubierto aunque
/// nadie llame a nada.
///
/// El dashboard de la Fase 5 se apoya entero en estas rutas. Construir encima de algo que nunca
/// se ha ejecutado es exactamente el error que se pagó con los comentarios, donde la interfaz
/// llamaba a un endpoint que no existía y las pruebas no lo veían.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReportingFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var cuerpo = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = cuerpo.GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    /// <summary>
    /// Un proyecto con una tarea dentro, creados por esta prueba.
    ///
    /// No se da por supuesto lo que haya sembrado el arranque ni lo que hayan dejado otras
    /// pruebas. Cuando se escribieron, tres de ellas fallaban porque no había ningún proyecto;
    /// al arreglar el fallo del inquilino empezaron a pasar, pero **por el motivo equivocado**:
    /// otras pruebas de la misma colección creaban proyectos antes. Una prueba que pasa por lo
    /// que hizo su vecina vuelve a fallar en cuanto cambia el orden, y entonces el fallo no
    /// señala a nada.
    /// </summary>
    private static async Task<Guid> CrearProyectoConTareaAsync(HttpClient cliente)
    {
        var alta = await cliente.PostAsJsonAsync("/api/v1/projects", new
        {
            spaceId = Guid.NewGuid(),
            name = "Proyecto para informes " + Guid.NewGuid(),
            description = "Creado por la prueba para que los informes tengan algo que contar",
            estimatedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        });
        alta.EnsureSuccessStatusCode();
        var proyecto = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var tarea = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = proyecto,
            title = "Tarea para informes",
            description = "Para que el desglose por estado no salga vacío",
            assigneeId = Guid.Empty,
            estimatedHours = 4,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
        });
        tarea.EnsureSuccessStatusCode();

        return proyecto;
    }

    /// <summary>
    /// Lo primero que había que comprobar y nadie había comprobado: que responden.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/reports")]
    [InlineData("/api/v1/reports/kpi")]
    [InlineData("/api/v1/reports/tasks/breakdown")]
    [InlineData("/api/v1/reports/projects/progress")]
    [InlineData("/api/v1/dashboards")]
    public async Task Las_rutas_de_informes_responden(string ruta)
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.GetAsync(ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            "es una de las rutas sobre las que se construye el dashboard");
    }

    /// <summary>
    /// Sin token no se ven los datos de nadie. Es la comprobación que más barata sale y más
    /// cara cuesta si falta.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/reports/kpi")]
    [InlineData("/api/v1/reports/tasks/breakdown")]
    [InlineData("/api/v1/reports/projects/progress")]
    [InlineData("/api/v1/dashboards")]
    public async Task Sin_autenticar_no_se_llega_a_los_informes(string ruta)
    {
        var respuesta = await factory.CreateClient().GetAsync(ruta);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Los KPI tienen que salir de los datos, no de una constante: la prueba crea un proyecto
    /// con una tarea, así que ninguno de los dos contadores puede seguir en cero.
    /// </summary>
    [Fact]
    public async Task Los_kpi_cuentan_lo_que_hay_de_verdad()
    {
        var cliente = await AutenticarAsync();
        await CrearProyectoConTareaAsync(cliente);

        var kpi = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");

        kpi.GetProperty("totalProjects").GetInt32().Should().BeGreaterThan(0);
        kpi.GetProperty("totalTasks").GetInt32().Should().BeGreaterThan(0);
    }

    /// <summary>
    /// El porcentaje de avance tiene que ser coherente con los dos contadores de los que sale.
    /// Un cociente que no cuadra con su numerador y su denominador delata que alguno de los tres
    /// no viene de donde dice.
    /// </summary>
    [Fact]
    public async Task El_avance_cuadra_con_las_tareas_hechas_y_el_total()
    {
        var cliente = await AutenticarAsync();
        await CrearProyectoConTareaAsync(cliente);

        var kpi = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");

        var total = kpi.GetProperty("totalTasks").GetInt32();
        var hechas = kpi.GetProperty("doneTasks").GetInt32();
        var avance = kpi.GetProperty("throughput").GetDouble();

        hechas.Should().BeLessThanOrEqualTo(total);
        avance.Should().BeApproximately((double)hechas / total * 100, 0.1);
    }

    /// <summary>
    /// El desglose por estado reparte las tareas: la suma no puede superar el total. Que sea
    /// menor sí es posible —hay estados fuera de los cuatro que el desglose contempla—, pero
    /// que sea mayor significaría que alguna tarea se cuenta dos veces.
    /// </summary>
    [Fact]
    public async Task El_desglose_por_estado_no_cuenta_ninguna_tarea_dos_veces()
    {
        var cliente = await AutenticarAsync();
        await CrearProyectoConTareaAsync(cliente);

        var kpi = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");
        var desglose = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/tasks/breakdown");

        var total = kpi.GetProperty("totalTasks").GetInt32();
        var sumaDelDesglose = desglose.EnumerateArray().Sum(e => e.GetProperty("count").GetInt32());

        sumaDelDesglose.Should().BeLessThanOrEqualTo(total);
    }

    /// <summary>Cada corte del desglose trae su color: el gráfico se pinta con esto.</summary>
    [Fact]
    public async Task Cada_corte_del_desglose_trae_estado_y_color()
    {
        var cliente = await AutenticarAsync();

        var desglose = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/tasks/breakdown");

        desglose.EnumerateArray().Should().NotBeEmpty();
        foreach (var corte in desglose.EnumerateArray())
        {
            corte.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
            corte.GetProperty("color").GetString().Should().MatchRegex("^#[0-9A-Fa-f]{6}$");
            corte.GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        }
    }

    /// <summary>
    /// El progreso por proyecto: cada fila cuadra consigo misma. Un porcentaje que no se
    /// corresponde con sus propios contadores es el síntoma de un cálculo inventado.
    /// </summary>
    [Fact]
    public async Task El_progreso_de_cada_proyecto_cuadra_con_sus_propias_tareas()
    {
        var cliente = await AutenticarAsync();
        await CrearProyectoConTareaAsync(cliente);

        var progreso = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/projects/progress");

        progreso.EnumerateArray().Should().NotBeEmpty("la prueba acaba de crear uno");

        foreach (var proyecto in progreso.EnumerateArray())
        {
            var total = proyecto.GetProperty("totalTasks").GetInt32();
            var hechas = proyecto.GetProperty("doneTasks").GetInt32();
            var pct = proyecto.GetProperty("completionPct").GetDouble();

            proyecto.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
            hechas.Should().BeLessThanOrEqualTo(total);
            pct.Should().BeInRange(0, 100);

            var esperado = total > 0 ? (double)hechas / total * 100 : 0;
            pct.Should().BeApproximately(esperado, 0.1);
        }
    }

    /// <summary>
    /// Los tiempos del panel no son constantes escritas en el código.
    ///
    /// Lo eran: 2,5 días de entrega y 1,4 de ciclo, fijos, porque la tarea no guardaba ninguna
    /// marca de tiempo y no había con qué calcularlos. La prueba cierra una tarea recién creada
    /// y exige que el tiempo de entrega salga muy pequeño —segundos, no días—, que es lo que de
    /// verdad tardó. Si alguien devolviera otra vez un número fijo, esto lo pilla.
    /// </summary>
    [Fact]
    public async Task El_tiempo_de_entrega_sale_de_las_fechas_y_no_de_una_constante()
    {
        var cliente = await AutenticarAsync();

        var alta = await cliente.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = Guid.NewGuid(),
            title = "Tarea que se cierra al momento",
            description = "Para medir un tiempo de entrega conocido",
            assigneeId = Guid.Empty,
            estimatedHours = 1,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        });
        alta.EnsureSuccessStatusCode();
        var tarea = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var cierre = await cliente.PatchAsJsonAsync($"/api/v1/tasks/{tarea}/move", new { newStatus = "Done" });
        cierre.EnsureSuccessStatusCode();

        var kpi = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");
        var entrega = kpi.GetProperty("avgLeadTimeDays");

        entrega.ValueKind.Should().NotBe(JsonValueKind.Null,
            "acaba de cerrarse una tarea, así que hay con qué calcular la media");
        entrega.GetDouble().Should().BeLessThan(1.0,
            "la tarea se creó y se cerró en la misma prueba: la media no puede dar los 2,5 días que devolvía la constante");
    }

    /// <summary>
    /// El tiempo de ciclo viaja como hueco, no como número.
    ///
    /// Mide desde que el trabajo empieza de verdad, y eso exige saber cuándo la tarea entró en
    /// «En Progreso». Sólo se guarda el estado actual, no su historial, así que no se puede
    /// calcular. Un hueco es la respuesta honesta; el 1,4 que había antes no lo era.
    /// </summary>
    [Fact]
    public async Task El_tiempo_de_ciclo_se_declara_desconocido_en_vez_de_inventarse()
    {
        var cliente = await AutenticarAsync();

        var kpi = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");

        kpi.GetProperty("avgCycleTimeDays").ValueKind.Should().Be(JsonValueKind.Null,
            "sin historial de cambios de estado no hay forma de medirlo, y un número inventado no se distingue de uno medido");
    }

    /// <summary>
    /// El diagrama de quemado cuenta tareas de verdad.
    ///
    /// El que había era una recta inventada —bajaba una tarea cada dos días pasara lo que
    /// pasara— y rellenaba el total a un mínimo de diez para que la línea «quedara bien». Un
    /// gráfico que no depende de los datos es una decoración con aspecto de medida.
    /// </summary>
    [Fact]
    public async Task El_quemado_no_baja_si_no_se_cierra_nada()
    {
        var cliente = await AutenticarAsync();
        var proyecto = await CrearProyectoConTareaAsync(cliente);

        var quemado = await cliente.GetFromJsonAsync<JsonElement>(
            $"/api/v1/reports/projects/{proyecto}/burndown");

        var puntos = quemado.GetProperty("data").EnumerateArray().ToList();
        puntos.Should().NotBeEmpty();

        var restantes = puntos.Select(p => p.GetProperty("remainingTasks").GetInt32()).ToList();

        restantes.Should().OnlyContain(r => r == restantes[0],
            "no se ha cerrado ninguna tarea del proyecto, así que lo que queda no puede bajar solo");
        restantes[0].Should().Be(1, "el proyecto tiene exactamente la tarea que creó la prueba");
    }

    /// <summary>
    /// Un informe que no existe es un 404, no un 200 con el cuerpo vacío. La diferencia importa:
    /// quien llama necesita distinguir «no está» de «está y no tiene nada».
    /// </summary>
    [Fact]
    public async Task Un_informe_que_no_existe_da_404()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.GetAsync($"/api/v1/reports/{Guid.NewGuid()}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>El listado de informes es una lista, aunque esté vacía. Nunca un 404.</summary>
    [Fact]
    public async Task El_listado_de_informes_responde_aunque_no_haya_ninguno()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.GetAsync("/api/v1/reports");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
