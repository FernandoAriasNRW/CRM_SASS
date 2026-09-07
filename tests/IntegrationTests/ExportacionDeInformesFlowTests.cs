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
public sealed class ExportacionDeInformesFlowTests(CrmApiFactory factory)
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
    private static readonly TimeSpan Paciencia = TimeSpan.FromSeconds(20);

    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    /// <summary>Un informe recién creado, para no depender de los que haya.</summary>
    private static async Task<Guid> CrearInformeAsync(HttpClient cliente, string tipo = "KpiSummary")
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports", new
        {
            Name = $"Informe de prueba {Guid.NewGuid():N}"[..40],
            Type = tipo,
            Format = "Csv"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created, await respuesta.Content.ReadAsStringAsync());

        var creado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return creado.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Espera a que la exportación deje de estar en marcha, y devuelve su estado final.
    ///
    /// Devuelve también las fallidas en vez de reventar: qué se hace con un fallo lo decide cada
    /// prueba, y hay una que precisamente comprueba que el fallo llega con su motivo.
    /// </summary>
    private static async Task<JsonElement> EsperarAsync(HttpClient cliente, Guid exportacionId)
    {
        var limite = DateTime.UtcNow + Paciencia;

        while (DateTime.UtcNow < limite)
        {
            var estado = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/exportaciones/{exportacionId}");
            var nombre = estado.GetProperty("estado").GetString();

            if (nombre is "Lista" or "Fallida") return estado;

            await Task.Delay(500);
        }

        throw new TimeoutException(
            $"La exportación {exportacionId} sigue sin terminar tras {Paciencia.TotalSeconds} segundos. "
            + "Si esto falla de verdad, es el caso que el plan señalaba: algo quedó en «generando» para siempre.");
    }

    #region El ciclo completo

    /// <summary>
    /// La prueba que faltaba: pedir, esperar, descargar, y que haya bytes dentro.
    /// </summary>
    [Fact]
    public async Task Una_exportacion_produce_un_fichero_que_se_puede_descargar()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var peticion = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Csv", null);

        // 202 y no 200: todavía no hay fichero, sólo la promesa de que lo habrá. Con 200 la
        // pantalla creería que ya puede descargar.
        peticion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var trabajo = await peticion.Content.ReadFromJsonAsync<JsonElement>();
        trabajo.GetProperty("estado").GetString().Should().Be("Pendiente");

        var exportacionId = trabajo.GetProperty("id").GetGuid();
        var final = await EsperarAsync(cliente, exportacionId);

        final.GetProperty("estado").GetString().Should().Be("Lista",
            "si falló, el motivo está aquí: " + (final.TryGetProperty("error", out var e) ? e.ToString() : "sin motivo"));

        final.GetProperty("tamanoBytes").GetInt64().Should().BeGreaterThan(0);

        var descarga = await cliente.GetAsync($"/api/v1/exportaciones/{exportacionId}/descargar");
        descarga.StatusCode.Should().Be(HttpStatusCode.OK);

        var bytes = await descarga.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeEmpty("antes esto era una URL inventada que no servía ningún endpoint");

        // Y el contenido es el informe, no un fichero cualquiera: el CSV de KPIs trae sus
        // encabezados.
        var texto = Encoding.UTF8.GetString(bytes);
        texto.Should().Contain("Indicador");
        texto.Should().Contain("Proyectos");
    }

    /// <summary>
    /// El nombre del fichero llega en la cabecera, para que el navegador lo use al guardarlo.
    ///
    /// Sin esto, el fichero se guarda con el identificador de la exportación por nombre y quien
    /// lo descarga acaba con «a3f2…csv» en la carpeta de descargas.
    /// </summary>
    [Fact]
    public async Task La_descarga_trae_nombre_y_tipo_de_contenido()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var peticion = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Excel", null);
        var exportacionId = (await peticion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await EsperarAsync(cliente, exportacionId);

        var descarga = await cliente.GetAsync($"/api/v1/exportaciones/{exportacionId}/descargar");

        descarga.Content.Headers.ContentType!.MediaType.Should()
            .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        descarga.Content.Headers.ContentDisposition!.FileNameStar.Should().EndWith(".xlsx");
    }

    [Theory]
    [InlineData("Csv")]
    [InlineData("Excel")]
    [InlineData("Pdf")]
    public async Task Los_tres_formatos_generan_fichero(string formato)
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var peticion = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format={formato}", null);
        var exportacionId = (await peticion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var final = await EsperarAsync(cliente, exportacionId);

        final.GetProperty("estado").GetString().Should().Be("Lista",
            $"el formato {formato} tiene que producir fichero. Motivo del fallo: "
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
    public async Task El_fichero_trae_los_mismos_numeros_que_la_api()
    {
        var cliente = await AutenticarAsync();

        // Lo que la aplicación enseña en pantalla.
        var kpi = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports/kpi");
        var tareasSegunLaApi = kpi.GetProperty("totalTasks").GetInt32();
        var proyectosSegunLaApi = kpi.GetProperty("totalProjects").GetInt32();

        tareasSegunLaApi.Should().BeGreaterThan(0,
            "sin datos esta prueba no distingue «bien» de «vacío», que es justo lo que vino a vigilar");

        // Y lo mismo, exportado por el trabajador de segundo plano.
        var informeId = await CrearInformeAsync(cliente, tipo: "KpiSummary");
        var peticion = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Csv", null);
        var exportacionId = (await peticion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await EsperarAsync(cliente, exportacionId);

        var descarga = await cliente.GetAsync($"/api/v1/exportaciones/{exportacionId}/descargar");
        var texto = Encoding.UTF8.GetString(await descarga.Content.ReadAsByteArrayAsync());

        texto.Should().Contain($"Tareas;{tareasSegunLaApi}",
            "el informe exportado y la pantalla salen del mismo motor de consulta, así que tienen "
            + "que decir lo mismo. Cuando no coincidían, el fichero decía 0");

        texto.Should().Contain($"Proyectos;{proyectosSegunLaApi}");
    }

    /// <summary>
    /// Y el detalle también trae filas, no sólo su cabecera.
    ///
    /// Misma causa que la de arriba, otro síntoma: el CSV de tareas salía con la línea de
    /// encabezados y nada debajo.
    /// </summary>
    [Fact]
    public async Task El_informe_de_detalle_trae_filas()
    {
        var cliente = await AutenticarAsync();

        var listado = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tasks?pageSize=1");
        listado.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0, "hacen falta tareas");

        var informeId = await CrearInformeAsync(cliente, tipo: "TaskSummary");
        var peticion = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Csv", null);
        var exportacionId = (await peticion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await EsperarAsync(cliente, exportacionId);

        var descarga = await cliente.GetAsync($"/api/v1/exportaciones/{exportacionId}/descargar");
        var texto = Encoding.UTF8.GetString(await descarga.Content.ReadAsByteArrayAsync());

        var lineas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        lineas.Should().HaveCountGreaterThan(1,
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
    public async Task Un_fallo_dice_por_que_y_no_desaparece()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente, tipo: "Custom");

        var peticion = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Csv", null);
        var exportacionId = (await peticion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var final = await EsperarAsync(cliente, exportacionId);

        final.GetProperty("estado").GetString().Should().Be("Fallida");

        final.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace(
            "un fallo mudo obliga a mirar los registros del servidor, y quien pregunta no tiene acceso");

        final.GetProperty("error").GetString().Should().Contain("constructor",
            "el motivo tiene que explicar qué hacer, no sólo que pasó algo");
    }

    /// <summary>Descargar algo que falló explica el motivo, en vez de dar un 404 desconcertante.</summary>
    [Fact]
    public async Task Descargar_una_fallida_explica_el_motivo()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente, tipo: "Custom");

        var peticion = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Csv", null);
        var exportacionId = (await peticion.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await EsperarAsync(cliente, exportacionId);

        var descarga = await cliente.GetAsync($"/api/v1/exportaciones/{exportacionId}/descargar");

        // 409 y no 404: la exportación existe, lo que pasa es que no salió. Un 404 haría pensar
        // que se perdió.
        descarga.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await descarga.Content.ReadAsStringAsync()).Should().Contain("falló");
    }

    /// <summary>
    /// Pulsar dos veces «exportar» no genera el fichero dos veces ni manda dos avisos.
    ///
    /// Es lo que pasa siempre: la pantalla no cambia enseguida y la gente vuelve a pulsar.
    /// </summary>
    [Fact]
    public async Task Pedir_lo_mismo_dos_veces_devuelve_el_mismo_trabajo()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var primera = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Pdf", null);
        var segunda = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Pdf", null);

        var una = (await primera.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var otra = (await segunda.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        otra.Should().Be(una, "el trabajo ya estaba en marcha; encolar otro duplicaría fichero y aviso");
    }

    /// <summary>Y pedir el mismo informe en otro formato sí son dos trabajos.</summary>
    [Fact]
    public async Task Dos_formatos_distintos_son_dos_trabajos()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var csv = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Csv", null);
        var pdf = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Pdf", null);

        var uno = (await csv.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var otro = (await pdf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        otro.Should().NotBe(uno);
    }

    #endregion

    #region Contrato

    [Fact]
    public async Task Un_formato_que_no_existe_dice_cuales_hay()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var respuesta = await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Word", null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var mensaje = await respuesta.Content.ReadAsStringAsync();
        mensaje.Should().Contain("Word");
        mensaje.Should().Contain("Pdf", "decir sólo «formato inválido» deja sin saber qué poner");
    }

    [Fact]
    public async Task Exportar_un_informe_que_no_existe_se_rechaza()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsync($"/api/v1/reports/{Guid.NewGuid()}/exportar?format=Csv", null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// La ruta antigua sigue viva y ahora exporta de verdad.
    ///
    /// Se conserva para no romper la pantalla mientras se actualiza, pero lo que se conserva es
    /// el nombre, no el comportamiento: fingir que se había generado algo era el fallo.
    /// </summary>
    [Fact]
    public async Task La_ruta_antigua_de_generar_ahora_encola_una_exportacion_real()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        var respuesta = await cliente.PostAsync($"/api/v1/reports/{informeId}/generate?format=Csv", null);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var exportacionId = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var final = await EsperarAsync(cliente, exportacionId);

        final.GetProperty("estado").GetString().Should().Be("Lista");
    }

    [Fact]
    public async Task Las_exportaciones_de_un_informe_se_pueden_listar()
    {
        var cliente = await AutenticarAsync();
        var informeId = await CrearInformeAsync(cliente);

        await cliente.PostAsync($"/api/v1/reports/{informeId}/exportar?format=Csv", null);

        var lista = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{informeId}/exportaciones");

        lista.EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Sin_autenticar_no_se_exporta_ni_se_descarga()
    {
        var anonimo = factory.CreateClient();

        (await anonimo.PostAsync($"/api/v1/reports/{Guid.NewGuid()}/exportar?format=Csv", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await anonimo.GetAsync($"/api/v1/exportaciones/{Guid.NewGuid()}/descargar"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
