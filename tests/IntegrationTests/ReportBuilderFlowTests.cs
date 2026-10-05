using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El constructor de informes, contra la API levantada.
///
/// <b>La prueba que da sentido a las demás es la del contrato:</b> todo lo que el catálogo
/// ofrece, el motor sabe resolverlo. Es la unión que este módulo ya rompió dos veces —el
/// desplegable de tipos y el filtro del menú— y aquí el daño sería peor, porque la definición se
/// guarda: el fallo aparecería al exportar, días después.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReportBuilderFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<Guid> CreateReportAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/reports", new
        {
            Name = $"A medida {Guid.NewGuid():N}"[..30],
            Type = "Custom",
            Format = "Csv"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static object Definition(
        string source = "Tasks", string groupBy = "status", string measure = "count",
        string visualization = "table", object[]? filters = null, string? granularidad = null)
        => new { dataSource = source, groupBy = groupBy, measure = measure, visualization = visualization, filters = filters ?? [], granularity = granularidad };

    #region El catálogo

    [Fact]
    public async Task The_catalog_brings_sources_fields_measures_and_operators()
    {
        var client = await AuthenticateAsync();

        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/catalog");

        catalog.GetProperty("dataSources").EnumerateArray().Should().NotBeEmpty();
        catalog.GetProperty("operators").EnumerateArray().Should().NotBeEmpty();
        catalog.GetProperty("visualizations").EnumerateArray().Should().NotBeEmpty();
        catalog.GetProperty("granularities").EnumerateArray().Should().NotBeEmpty();

        var tasks = catalog.GetProperty("dataSources").EnumerateArray()
            .Single(o => o.GetProperty("key").GetString() == "Tasks");

        tasks.GetProperty("fields").EnumerateArray().Should().NotBeEmpty();
        tasks.GetProperty("measures").EnumerateArray().Should().NotBeEmpty();
    }

    /// <summary>
    /// <b>Todo lo que el catálogo ofrece, el motor lo sabe resolver.</b>
    ///
    /// Recorre cada origen, cada campo y cada medida y pide la vista previa. Si alguna combinación
    /// no estuviera implementada, la pantalla la ofrecería igualmente —porque se alimenta del
    /// catálogo— y el informe fallaría al generarse, cuando quien lo construyó ya no está.
    ///
    /// Es la misma comprobación que <c>ReportContractTests</c> hace con los tipos de informe,
    /// llevada a una superficie veinte veces mayor.
    /// </summary>
    [Fact]
    public async Task Everything_the_catalog_offers_can_be_computed()
    {
        var client = await AuthenticateAsync();
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/catalog");

        var failures = new List<string>();

        foreach (var source in catalog.GetProperty("dataSources").EnumerateArray())
        {
            var key = source.GetProperty("key").GetString()!;

            foreach (var field in source.GetProperty("fields").EnumerateArray())
            {
                var fieldKey = field.GetProperty("key").GetString()!;
                var isDate = field.GetProperty("type").GetString() == "Date";

                foreach (var measure in source.GetProperty("measures").EnumerateArray())
                {
                    var measureKey = measure.GetProperty("key").GetString()!;

                    var response = await client.PostAsJsonAsync("/api/v1/reports/preview", new
                    {
                        definition = Definition(key, fieldKey, measureKey,
                                                granularidad: isDate ? "month" : null),
                        title = "Contrato"
                    });

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        failures.Add($"{key}/{fieldKey}/{measureKey}: "
                                   + await response.Content.ReadAsStringAsync());
                    }
                }
            }
        }

        failures.Should().BeEmpty(
            "el catálogo es lo que la pantalla ofrece; lo que ofrece tiene que poder calcularse");
    }

    /// <summary>
    /// Y cada operador se puede aplicar a cada campo de su tipo.
    ///
    /// El catálogo dice qué operadores valen para qué tipos; esta prueba comprueba que el motor
    /// los traduce todos. Un operador que la pantalla ofrece y el motor no traduce produce un
    /// informe que revienta al filtrar.
    /// </summary>
    [Fact]
    public async Task Every_operator_applies_to_the_fields_of_its_type()
    {
        var client = await AuthenticateAsync();
        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/catalog");

        var operators = catalog.GetProperty("operators").EnumerateArray().ToList();
        var failures = new List<string>();

        foreach (var source in catalog.GetProperty("dataSources").EnumerateArray())
        {
            var key = source.GetProperty("key").GetString()!;
            var groupBy = source.GetProperty("fields").EnumerateArray().First();
            var groupByKey = groupBy.GetProperty("key").GetString()!;
            var groupByIsDate = groupBy.GetProperty("type").GetString() == "Date";

            foreach (var field in source.GetProperty("fields").EnumerateArray())
            {
                var fieldKey = field.GetProperty("key").GetString()!;
                var type = field.GetProperty("type").GetString()!;

                // Los operadores **de este campo**, que el catálogo resuelve: el tipo no basta,
                // porque «está vacío» sólo vale sobre campos que pueden no tener valor.
                var own = field.GetProperty("operators").EnumerateArray()
                    .Select(o => o.GetString()).ToHashSet();

                foreach (var op in operators.Where(o => own.Contains(o.GetProperty("key").GetString())))
                {
                    var opKey = op.GetProperty("key").GetString()!;

                    // Si el campo es una lista cerrada, se usa uno de sus valores reales. Antes
                    // se mandaba una cadena cualquiera y el servidor la rechazaba —con razón—,
                    // que es lo que destapó que la pantalla obligaba a escribir «Open» a mano.
                    var values = field.GetProperty("values").EnumerateArray()
                        .Select(v => v.GetString()!).ToList();

                    var value = op.GetProperty("needsValue").GetBoolean()
                        ? values.FirstOrDefault() ?? SampleValue(type)
                        : null;

                    var response = await client.PostAsJsonAsync("/api/v1/reports/preview", new
                    {
                        definition = Definition(
                            key, groupByKey, "count",
                            filters: [new { field = fieldKey, @operator = opKey, value = value }],
                            granularidad: groupByIsDate ? "month" : null),
                        title = "Contrato de operadores"
                    });

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        failures.Add($"{key}/{fieldKey} ({type}) {opKey}: "
                                   + await response.Content.ReadAsStringAsync());
                    }
                }
            }
        }

        failures.Should().BeEmpty();
    }

    /// <summary>Un valor plausible según el tipo, para que el filtro se pueda evaluar.</summary>
    private static string SampleValue(string type) => type switch
    {
        "Number" => "1",
        "Date" => "2020-01-01",
        "Person" or "Reference" => Guid.Empty.ToString(),
        _ => "algo"
    };

    #endregion

    #region Vista previa

    [Fact]
    public async Task The_preview_returns_columns_and_rows()
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/reports/preview", new
        {
            definition = Definition("Tasks", "status", "count"),
            title = "Tareas por estado"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();

        preview.GetProperty("columns").EnumerateArray().Should().HaveCount(2);
        preview.GetProperty("rows").EnumerateArray().Should().NotBeEmpty("el sembrador crea tareas");
        preview.GetProperty("subtitle").GetString().Should().Contain("Tareas");
    }

    /// <summary>
    /// El subtítulo dice qué filtros se aplicaron.
    ///
    /// Un informe exportado circula por correo y acaba en una reunión sin quien lo generó
    /// delante. Sin esta línea, nadie puede distinguir «tickets por estado» de «tickets por
    /// estado, sólo los urgentes».
    /// </summary>
    [Fact]
    public async Task The_subtitle_records_the_filters()
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/reports/preview", new
        {
            definition = Definition("Tickets", "status", "count",
                filters: [new { field = "priority", @operator = "is", value = "High" }]),
            title = "Tickets urgentes"
        });

        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();

        preview.GetProperty("subtitle").GetString().Should().Contain("Prioridad").And.Contain("High");
    }

    /// <summary>Filtrar de verdad cambia el resultado; si no, el filtro sería un adorno.</summary>
    [Fact]
    public async Task A_filter_changes_the_result()
    {
        var client = await AuthenticateAsync();

        var withoutFilter = await Total(client, Definition("Tickets", "status", "count"));

        var withFilter = await Total(client, Definition("Tickets", "status", "count",
            filters: [new { field = "priority", @operator = "is", value = "High" }]));

        withoutFilter.Should().BeGreaterThan(0);
        withFilter.Should().BeLessThan(withoutFilter, "filtrar por una prioridad deja fuera las demás");
    }

    private static async Task<int> Total(HttpClient client, object definition)
    {
        var response = await client.PostAsJsonAsync("/api/v1/reports/preview",
            new { definition = definition, title = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Se suman los valores de la segunda columna: es la medida.
        return preview.GetProperty("rows").EnumerateArray()
            .Sum(f => int.Parse(f.EnumerateArray().Last().GetString()!));
    }

    /// <summary>
    /// Una media sin nada que promediar escribe una raya, no un cero.
    ///
    /// «0 días medios hasta resolver» dice que se resuelve al instante; la verdad, cuando no hay
    /// ningún ticket resuelto, es que no hay nada que medir. Son dos afirmaciones distintas y sólo
    /// una es cierta.
    ///
    /// Este proyecto ya tuvo esta mentira exacta en el tiempo medio de entrega del panel, que
    /// devolvía 0 en vez de un hueco. Se arregló allí y volvía a colarse aquí.
    /// </summary>
    [Fact]
    public async Task An_average_without_data_writes_a_dash_not_a_zero()
    {
        var client = await AuthenticateAsync();

        // Se piden los días medios hasta resolver **de los tickets sin resolver**: por definición
        // no hay ninguno que promediar.
        var response = await client.PostAsJsonAsync("/api/v1/reports/preview", new
        {
            definition = Definition("Tickets", "status", "avg_days_to_resolve",
                filters: [new { field = "resolved_at", @operator = "empty", value = (string?)null }]),
            title = "Sin resolver"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rows = preview.GetProperty("rows").EnumerateArray().ToList();

        rows.Should().NotBeEmpty("hay tickets sin resolver");

        foreach (var row in rows)
        {
            row.EnumerateArray().Last().GetString().Should().Be("—",
                "sin nada que promediar, un cero diría que se resuelve al instante");
        }
    }

    /// <summary>
    /// Una serie temporal sale en orden cronológico, no de mayor a menor.
    ///
    /// El motor ordenaba **siempre** por cantidad, que parece razonable —lo grande primero— y
    /// destroza cualquier serie temporal: «tickets por mes» salía 2026-08, 2026-09, 2026-07, y una
    /// gráfica de líneas con el eje de tiempo desordenado no significa nada.
    ///
    /// No lo cazó ninguna prueba: se vio mirando la salida real del panel. Ésta existe para que no
    /// haga falta volver a mirarla.
    /// </summary>
    [Fact]
    public async Task Grouping_by_date_comes_in_chronological_order()
    {
        var client = await AuthenticateAsync();

        // Por vencimiento y por día: el sembrador crea todos los tickets el mismo mes, así que
        // agrupándolos por mes sale un solo grupo y la prueba no comprobaría nada. Los
        // vencimientos de las tareas sí se reparten en varios días.
        var response = await client.PostAsJsonAsync("/api/v1/reports/preview", new
        {
            definition = Definition("Tasks", "due_date", "count", "line", granularidad: "day"),
            title = "Tareas por día de vencimiento"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();

        var days = preview.GetProperty("rows").EnumerateArray()
            .Select(f => f.EnumerateArray().First().GetString()!)
            .ToList();

        days.Should().HaveCountGreaterThan(1, "hacen falta varias fechas para que el orden importe");

        // Las claves llevan el año delante —«2026-08»— justo para que el orden alfabético sea el
        // cronológico. Si eso cambiara, esta comprobación dejaría de valer y hay que cambiarla
        // a la vez.
        days.Should().BeInAscendingOrder(StringComparer.Ordinal,
            "una serie temporal desordenada no se puede leer en una gráfica de líneas");
    }

    [Fact]
    public async Task An_invalid_definition_says_what_fails_and_what_is_valid()
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/reports/preview", new
        {
            definition = Definition("Tasks", "agent", "count"),
            title = "Imposible"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var message = await response.Content.ReadAsStringAsync();
        message.Should().Contain("agent", "hay que decir qué se pidió");
        message.Should().Contain("status", "y qué se podría haber pedido");
    }

    #endregion

    #region Guardar y exportar

    /// <summary>
    /// El recorrido completo: construir, guardar y exportar.
    ///
    /// Antes de esto, un informe de tipo «Custom» se rechazaba al exportar diciendo que el
    /// constructor no existía. Ahora existe, y lo que se guarda es lo que se exporta.
    /// </summary>
    [Fact]
    public async Task A_custom_report_is_saved_and_exported_as_its_definition_says()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var saved = await client.PutAsJsonAsync(
            $"/api/v1/reports/{reportId}/definition", Definition("Tickets", "priority", "count", "bar"));

        saved.StatusCode.Should().Be(HttpStatusCode.NoContent, await saved.Content.ReadAsStringAsync());

        // Se relee en otra petición: es la lección del PATCH que respondía 200 sin guardar.
        var read = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{reportId}/definition");
        read.GetProperty("dataSource").GetString().Should().Be("Tickets");
        read.GetProperty("groupBy").GetString().Should().Be("priority");
        read.GetProperty("visualization").GetString().Should().Be("bar");

        // Lo que se lee es exactamente lo que se guarda: sin propiedades calculadas coladas.
        // `filtrosAplicados` y `gruposEfectivos` son atajos de lectura del dominio y salían en la
        // respuesta, así que reenviar ese JSON mandaba campos que el servidor ignora.
        read.TryGetProperty("appliedFilters", out _).Should().BeFalse();
        read.TryGetProperty("gruposEfectivos", out _).Should().BeFalse();

        // Y el fichero exportado trae esa agrupación, no otra.
        var request = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Csv", null);
        request.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var exportId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var final = await WaitAsync(client, exportId);

        final.GetProperty("status").GetString().Should().Be("Ready",
            "motivo: " + (final.TryGetProperty("error", out var e) ? e.ToString() : "ninguno"));

        var download = await client.GetAsync($"/api/v1/exports/{exportId}/download");
        var text = Encoding.UTF8.GetString(await download.Content.ReadAsByteArrayAsync());

        text.Should().Contain("Prioridad", "la columna sale de la agrupación que se guardó");
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCountGreaterThan(1);
    }

    /// <summary>
    /// Guardar una definición imposible se rechaza **al guardar**, no al exportar.
    ///
    /// Es la razón de que la validación esté en el dominio y no sólo en el borde: un informe roto
    /// que se guarda bien falla más tarde, cuando quien lo construyó ya no está mirando.
    /// </summary>
    [Fact]
    public async Task An_impossible_definition_is_never_saved()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/reports/{reportId}/definition",
            Definition("Projects", "status", "sum_estimated_hours"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("sum_estimated_hours");
    }

    /// <summary>
    /// Un informe a medida sin definición lo dice, en vez de exportar un fichero vacío.
    ///
    /// Un PDF con encabezados y nada dentro parece un fallo del sistema; esto es un informe a
    /// medio configurar, que es otra cosa y se arregla de otra manera.
    /// </summary>
    [Fact]
    public async Task A_custom_report_without_definition_says_so()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var request = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Csv", null);
        var exportId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var final = await WaitAsync(client, exportId);

        final.GetProperty("status").GetString().Should().Be("Failed");
        final.GetProperty("error").GetString().Should().Contain("constructor");
    }

    #endregion

    private static async Task<JsonElement> WaitAsync(HttpClient client, Guid exportId)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < deadline)
        {
            var status = await client.GetFromJsonAsync<JsonElement>($"/api/v1/exports/{exportId}");
            if (status.GetProperty("status").GetString() is "Ready" or "Failed") return status;

            await Task.Delay(500);
        }

        throw new TimeoutException($"La exportación {exportId} no terminó a tiempo");
    }
}
