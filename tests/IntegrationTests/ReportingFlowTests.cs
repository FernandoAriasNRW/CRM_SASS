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

    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
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
    private static async Task<Guid> CreateProjectWithTaskAsync(HttpClient client)
    {
        var creation = await client.PostAsJsonAsync("/api/v1/projects", new
        {
            spaceId = Guid.NewGuid(),
            name = "Proyecto para informes " + Guid.NewGuid(),
            description = "Creado por la prueba para que los informes tengan algo que contar",
            estimatedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        });
        creation.EnsureSuccessStatusCode();
        var project = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var task = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = project,
            title = "Tarea para informes",
            description = "Para que el desglose por estado no salga vacío",
            assigneeId = Guid.Empty,
            estimatedHours = 4,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
        });
        task.EnsureSuccessStatusCode();

        return project;
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
    public async Task Report_routes_answer(string path)
    {
        var client = await AuthenticateAsync();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
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
    public async Task Without_authentication_reports_are_unreachable(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Los KPI tienen que salir de los datos, no de una constante: la prueba crea un proyecto
    /// con una tarea, así que ninguno de los dos contadores puede seguir en cero.
    /// </summary>
    [Fact]
    public async Task Kpis_count_what_really_exists()
    {
        var client = await AuthenticateAsync();
        await CreateProjectWithTaskAsync(client);

        var kpi = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");

        kpi.GetProperty("totalProjects").GetInt32().Should().BeGreaterThan(0);
        kpi.GetProperty("totalTasks").GetInt32().Should().BeGreaterThan(0);
    }

    /// <summary>
    /// El porcentaje de avance tiene que ser coherente con los dos contadores de los que sale.
    /// Un cociente que no cuadra con su numerador y su denominador delata que alguno de los tres
    /// no viene de donde dice.
    /// </summary>
    [Fact]
    public async Task Progress_matches_done_and_total_tasks()
    {
        var client = await AuthenticateAsync();
        await CreateProjectWithTaskAsync(client);

        var kpi = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");

        var total = kpi.GetProperty("totalTasks").GetInt32();
        var done = kpi.GetProperty("doneTasks").GetInt32();
        var progress = kpi.GetProperty("throughput").GetDouble();

        done.Should().BeLessThanOrEqualTo(total);
        progress.Should().BeApproximately((double)done / total * 100, 0.1);
    }

    /// <summary>
    /// El desglose por estado reparte las tareas: la suma no puede superar el total. Que sea
    /// menor sí es posible —hay estados fuera de los cuatro que el desglose contempla—, pero
    /// que sea mayor significaría que alguna tarea se cuenta dos veces.
    /// </summary>
    [Fact]
    public async Task The_status_breakdown_counts_no_task_twice()
    {
        var client = await AuthenticateAsync();
        await CreateProjectWithTaskAsync(client);

        var kpi = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");
        var breakdown = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/tasks/breakdown");

        var total = kpi.GetProperty("totalTasks").GetInt32();
        var breakdownSum = breakdown.EnumerateArray().Sum(e => e.GetProperty("count").GetInt32());

        breakdownSum.Should().BeLessThanOrEqualTo(total);
    }

    /// <summary>Cada corte del desglose trae su color: el gráfico se pinta con esto.</summary>
    [Fact]
    public async Task Each_breakdown_slice_has_status_and_color()
    {
        var client = await AuthenticateAsync();

        var breakdown = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/tasks/breakdown");

        breakdown.EnumerateArray().Should().NotBeEmpty();
        foreach (var slice in breakdown.EnumerateArray())
        {
            slice.GetProperty("status").GetString().Should().NotBeNullOrWhiteSpace();
            slice.GetProperty("color").GetString().Should().MatchRegex("^#[0-9A-Fa-f]{6}$");
            slice.GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        }
    }

    /// <summary>
    /// El progreso por proyecto: cada fila cuadra consigo misma. Un porcentaje que no se
    /// corresponde con sus propios contadores es el síntoma de un cálculo inventado.
    /// </summary>
    [Fact]
    public async Task Each_project_progress_matches_its_own_tasks()
    {
        var client = await AuthenticateAsync();
        await CreateProjectWithTaskAsync(client);

        var progressValue = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/projects/progress");

        progressValue.EnumerateArray().Should().NotBeEmpty("la prueba acaba de crear uno");

        foreach (var project in progressValue.EnumerateArray())
        {
            var total = project.GetProperty("totalTasks").GetInt32();
            var done = project.GetProperty("doneTasks").GetInt32();
            var pct = project.GetProperty("completionPct").GetDouble();

            project.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
            done.Should().BeLessThanOrEqualTo(total);
            pct.Should().BeInRange(0, 100);

            var expected = total > 0 ? (double)done / total * 100 : 0;
            pct.Should().BeApproximately(expected, 0.1);
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
    public async Task Lead_time_comes_from_the_dates_not_a_constant()
    {
        var client = await AuthenticateAsync();

        var creation = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = await TestProjects.CreateAsync(client),
            title = "Tarea que se cierra al momento",
            description = "Para medir un tiempo de entrega conocido",
            assigneeId = Guid.Empty,
            estimatedHours = 1,
            dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
        });
        creation.EnsureSuccessStatusCode();
        var task = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var closing = await client.PatchAsJsonAsync($"/api/v1/tasks/{task}/move", new { newStatus = "Done" });
        closing.EnsureSuccessStatusCode();

        var kpi = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");
        var leadTime = kpi.GetProperty("avgLeadTimeDays");

        leadTime.ValueKind.Should().NotBe(JsonValueKind.Null,
            "acaba de cerrarse una tarea, así que hay con qué calcular la media");
        leadTime.GetDouble().Should().BeLessThan(1.0,
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
    public async Task Cycle_time_is_reported_unknown_instead_of_invented()
    {
        var client = await AuthenticateAsync();

        var kpi = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");

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
    public async Task The_burndown_does_not_drop_if_nothing_closes()
    {
        var client = await AuthenticateAsync();
        var project = await CreateProjectWithTaskAsync(client);

        var burndown = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/reports/projects/{project}/burndown");

        var items = burndown.GetProperty("data").EnumerateArray().ToList();
        items.Should().NotBeEmpty();

        var remaining = items.Select(p => p.GetProperty("remainingTasks").GetInt32()).ToList();

        remaining.Should().OnlyContain(r => r == remaining[0],
            "no se ha cerrado ninguna tarea del proyecto, así que lo que queda no puede bajar solo");
        remaining[0].Should().Be(1, "el proyecto tiene exactamente la tarea que creó la prueba");
    }

    /// <summary>
    /// Un informe que no existe es un 404, no un 200 con el cuerpo vacío. La diferencia importa:
    /// quien llama necesita distinguir «no está» de «está y no tiene nada».
    /// </summary>
    [Fact]
    public async Task A_missing_report_returns_404()
    {
        var client = await AuthenticateAsync();

        var response = await client.GetAsync($"/api/v1/reports/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>El listado de informes es una lista, aunque esté vacía. Nunca un 404.</summary>
    [Fact]
    public async Task The_report_listing_answers_even_when_empty()
    {
        var client = await AuthenticateAsync();

        var response = await client.GetAsync("/api/v1/reports");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
