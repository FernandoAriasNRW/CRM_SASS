using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Exportar un informe, de principio a fin y contra la API levantada.
///
/// <b>Lo que estas pruebas vigilan es que exista fichero.</b> El camino anterior respondía 200 a
/// «generar», marcaba el informe como generado y guardaba una URL inventada
/// —<c>/reports/{id}/{nombre}.pdf</c>— que no apuntaba a nada y que ningún endpoint servía. Todo
/// lo que se podía comprobar desde fuera salía en verde; lo único que faltaba era el fichero.
///
/// Por eso aquí no basta con «responde 202»: se espera a que el trabajo termine y <b>se descarga
/// y se mira el contenido</b>.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReportExportFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    /// <summary>
    /// Cuánto se espera a que el trabajador de segundo plano haga su trabajo.
    ///
    /// El trabajador sondea cada cinco segundos, así que veinte da margen para dos vueltas sin
    /// dejar la prueba colgada si algo va mal. Se sondea en vez de dormir un rato fijo para que
    /// la prueba tarde lo que tarde el trabajo y no siempre lo peor.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    /// <summary>Un informe recién creado, para no depender de los que haya.</summary>
    private static async Task<Guid> CreateReportAsync(HttpClient client, string type = "KpiSummary")
    {
        var response = await client.PostAsJsonAsync("/api/v1/reports", new
        {
            Name = $"Informe de prueba {Guid.NewGuid():N}"[..40],
            Type = type,
            Format = "Csv"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Espera a que la exportación deje de estar en marcha, y devuelve su estado final.
    ///
    /// Devuelve también las fallidas en vez de reventar: qué se hace con un fallo lo decide cada
    /// prueba, y hay una que precisamente comprueba que el fallo llega con su motivo.
    /// </summary>
    private static async Task<JsonElement> WaitAsync(HttpClient client, Guid exportId)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (DateTime.UtcNow < deadline)
        {
            var status = await client.GetFromJsonAsync<JsonElement>($"/api/v1/exports/{exportId}");
            var name = status.GetProperty("status").GetString();

            if (name is "Ready" or "Failed") return status;

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"La exportación {exportId} sigue sin terminar tras {Patience.TotalSeconds} segundos. "
            + "Si esto falla de verdad, es el caso que el plan señalaba: algo quedó en «generando» para siempre.");
    }

    #region El ciclo completo

    /// <summary>
    /// La prueba que faltaba: pedir, esperar, descargar, y que haya bytes dentro.
    /// </summary>
    [Fact]
    public async Task An_export_produces_a_downloadable_file()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var request = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Csv", null);

        // 202 y no 200: todavía no hay fichero, sólo la promesa de que lo habrá. Con 200 la
        // pantalla creería que ya puede descargar.
        request.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var job = await request.Content.ReadFromJsonAsync<JsonElement>();
        job.GetProperty("status").GetString().Should().Be("Pending");

        var exportId = job.GetProperty("id").GetGuid();
        var final = await WaitAsync(client, exportId);

        final.GetProperty("status").GetString().Should().Be("Ready",
            "si falló, el motivo está aquí: " + (final.TryGetProperty("error", out var e) ? e.ToString() : "sin motivo"));

        final.GetProperty("sizeBytes").GetInt64().Should().BeGreaterThan(0);

        var download = await client.GetAsync($"/api/v1/exports/{exportId}/download");
        download.StatusCode.Should().Be(HttpStatusCode.OK);

        var bytes = await download.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty("antes esto era una URL inventada que no servía ningún endpoint");

        // Y el contenido es el informe, no un fichero cualquiera: el CSV de KPIs trae sus
        // encabezados.
        var text = Encoding.UTF8.GetString(bytes);
        text.Should().Contain("Indicador");
        text.Should().Contain("Proyectos");
    }

    /// <summary>
    /// El nombre del fichero llega en la cabecera, para que el navegador lo use al guardarlo.
    ///
    /// Sin esto, el fichero se guarda con el identificador de la exportación por nombre y quien
    /// lo descarga acaba con «a3f2…csv» en la carpeta de descargas.
    /// </summary>
    [Fact]
    public async Task The_download_has_name_and_content_type()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var request = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Excel", null);
        var exportId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await WaitAsync(client, exportId);

        var download = await client.GetAsync($"/api/v1/exports/{exportId}/download");

        download.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        download.Content.Headers.ContentDisposition!.FileNameStar.Should().EndWith(".xlsx");
    }

    [Theory]
    [InlineData("Csv")]
    [InlineData("Excel")]
    [InlineData("Pdf")]
    public async Task The_three_formats_produce_a_file(string format)
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var request = await client.PostAsync($"/api/v1/reports/{reportId}/export?format={format}", null);
        var exportId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var final = await WaitAsync(client, exportId);

        final.GetProperty("status").GetString().Should().Be("Ready",
            $"el formato {format} tiene que producir fichero. Motivo del fallo: "
            + (final.TryGetProperty("error", out var e) ? e.ToString() : "ninguno"));
    }

    /// <summary>
    /// El fichero trae los <b>mismos números</b> que la API, no sólo los encabezados correctos.
    ///
    /// Esta prueba existe por un fallo que las otras dejaron pasar. El trabajador de segundo
    /// plano no tiene petición y por tanto no tiene usuario, así que el filtro global de
    /// inquilino comparaba contra <c>Guid.Empty</c> y <b>todas las consultas devolvían cero
    /// filas</b>. El resultado: ficheros bien formados, con su aviso de «ya está listo», y con
    /// ceros donde la API enseñaba 15 tareas y 215 tickets. Nada fallaba.
    ///
    /// Las pruebas de arriba no lo vieron porque comprobaban que apareciera «Proyectos» —el
    /// encabezado— y no lo que ponía al lado. Comprobar que un informe tiene la forma correcta no
    /// es comprobar que dice la verdad.
    /// </summary>
    [Fact]
    public async Task The_file_has_the_same_numbers_as_the_api()
    {
        var client = await AuthenticateAsync();

        // Lo que la aplicación enseña en pantalla.
        var kpi = await client.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");
        var tasksPerApi = kpi.GetProperty("totalTasks").GetInt32();
        var projectsPerApi = kpi.GetProperty("totalProjects").GetInt32();

        tasksPerApi.Should().BeGreaterThan(0,
            "sin datos esta prueba no distingue «bien» de «vacío», que es justo lo que vino a vigilar");

        // Y lo mismo, exportado por el trabajador de segundo plano.
        var reportId = await CreateReportAsync(client, type: "KpiSummary");
        var request = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Csv", null);
        var exportId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await WaitAsync(client, exportId);

        var download = await client.GetAsync($"/api/v1/exports/{exportId}/download");
        var text = Encoding.UTF8.GetString(await download.Content.ReadAsByteArrayAsync());

        text.Should().Contain($"Tareas;{tasksPerApi}",
            "el informe exportado y la pantalla salen del mismo motor de consulta, así que tienen "
            + "que decir lo mismo. Cuando no coincidían, el fichero decía 0");

        text.Should().Contain($"Proyectos;{projectsPerApi}");
    }

    /// <summary>
    /// Y el detalle también trae filas, no sólo su cabecera.
    ///
    /// Misma causa que la de arriba, otro síntoma: el CSV de tareas salía con la línea de
    /// encabezados y nada debajo.
    /// </summary>
    [Fact]
    public async Task The_detail_report_has_rows()
    {
        var client = await AuthenticateAsync();

        var listing = await client.GetFromJsonAsync<JsonElement>("/api/v1/tasks?pageSize=1");
        listing.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0, "hacen falta tareas");

        var reportId = await CreateReportAsync(client, type: "TaskSummary");
        var request = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Csv", null);
        var exportId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await WaitAsync(client, exportId);

        var download = await client.GetAsync($"/api/v1/exports/{exportId}/download");
        var text = Encoding.UTF8.GetString(await download.Content.ReadAsByteArrayAsync());

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCountGreaterThan(1,
            "la primera línea son los encabezados; si es la única, el informe salió vacío");
    }

    #endregion

    #region Lo que se pudre si no se cuida

    /// <summary>
    /// Un fallo deja el motivo y queda visible, en vez de desaparecer.
    ///
    /// Era la advertencia expresa del plan. Se provoca con un informe a medida **sin definición**:
    /// no hay nada que calcular, el trabajador falla, y tiene que contarlo con palabras que digan
    /// qué hacer.
    ///
    /// Antes esta prueba se apoyaba en que los informes a medida no se podían exportar en
    /// absoluto. Ahora sí se pueden —el constructor existe— así que lo que se provoca es el caso
    /// que sigue sin poder resolverse: uno a medio configurar.
    /// </summary>
    [Fact]
    public async Task A_failure_says_why_and_does_not_disappear()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client, type: "Custom");

        var request = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Csv", null);
        var exportId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var final = await WaitAsync(client, exportId);

        final.GetProperty("status").GetString().Should().Be("Failed");

        final.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace(
            "un fallo mudo obliga a mirar los registros del servidor, y quien pregunta no tiene acceso");

        final.GetProperty("error").GetString().Should().Contain("constructor",
            "el motivo tiene que explicar qué hacer, no sólo que pasó algo");
    }

    /// <summary>Descargar algo que falló explica el motivo, en vez de dar un 404 desconcertante.</summary>
    [Fact]
    public async Task Downloading_a_failed_one_explains_why()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client, type: "Custom");

        var request = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Csv", null);
        var exportId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await WaitAsync(client, exportId);

        var download = await client.GetAsync($"/api/v1/exports/{exportId}/download");

        // 409 y no 404: la exportación existe, lo que pasa es que no salió. Un 404 haría pensar
        // que se perdió.
        download.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await download.Content.ReadAsStringAsync()).Should().Contain("falló");
    }

    /// <summary>
    /// Pulsar dos veces «exportar» no genera el fichero dos veces ni manda dos avisos.
    ///
    /// Es lo que pasa siempre: la pantalla no cambia enseguida y la gente vuelve a pulsar.
    /// </summary>
    [Fact]
    public async Task Asking_twice_returns_the_same_job()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var first = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Pdf", null);
        var second = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Pdf", null);

        var oneExport = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var other = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        other.Should().Be(oneExport, "el trabajo ya estaba en marcha; encolar otro duplicaría fichero y aviso");
    }

    /// <summary>Y pedir el mismo informe en otro formato sí son dos trabajos.</summary>
    [Fact]
    public async Task Two_formats_are_two_jobs()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var csv = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Csv", null);
        var pdf = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Pdf", null);

        var one = (await csv.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var other = (await pdf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        other.Should().NotBe(one);
    }

    #endregion

    #region Contrato

    [Fact]
    public async Task An_unknown_format_lists_the_available_ones()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var response = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Word", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var message = await response.Content.ReadAsStringAsync();
        message.Should().Contain("Word");
        message.Should().Contain("Pdf", "decir sólo «formato inválido» deja sin saber qué poner");
    }

    [Fact]
    public async Task Exporting_a_missing_report_is_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsync($"/api/v1/reports/{Guid.NewGuid()}/export?format=Csv", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// La ruta antigua sigue viva y ahora exporta de verdad.
    ///
    /// Se conserva para no romper la pantalla mientras se actualiza, pero lo que se conserva es
    /// el nombre, no el comportamiento: fingir que se había generado algo era el fallo.
    /// </summary>
    [Fact]
    public async Task The_old_generate_route_now_queues_a_real_export()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var response = await client.PostAsync($"/api/v1/reports/{reportId}/generate?format=Csv", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var exportId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var final = await WaitAsync(client, exportId);

        final.GetProperty("status").GetString().Should().Be("Ready");
    }

    [Fact]
    public async Task A_report_exports_can_be_listed()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Csv", null);

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{reportId}/exports");

        list.EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Without_authentication_nothing_is_exported_or_downloaded()
    {
        var anonymous = factory.CreateClient();

        (await anonymous.PostAsync($"/api/v1/reports/{Guid.NewGuid()}/export?format=Csv", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await anonymous.GetAsync($"/api/v1/exports/{Guid.NewGuid()}/download"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
