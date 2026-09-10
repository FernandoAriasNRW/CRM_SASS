using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El panel de inicio, contra la API levantada.
///
/// <b>Lo que vigilan estas pruebas es que el panel enseñe datos de verdad.</b> El plan lo decía
/// con todas las letras: «un dashboard que enseña una gráfica bonita con datos inventados es la
/// peor pantalla posible de todo el producto: se toman decisiones con ella».
///
/// Y hay un antecedente concreto: la pantalla ya tenía un gestor de paneles donde se podían crear,
/// nombrar y marcar como públicos, y <b>pulsarlos no hacía nada</b> — la selección se guardaba en
/// una señal que no pintaba nada, y la columna de widgets no la leía nadie. Nunca llegó a haber
/// una sola fila en esa tabla.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PanelFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static async Task<JsonElement> MiPanelAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/v1/dashboards/mio");
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    #region Arranque

    /// <summary>
    /// La primera vez, el panel se crea con sus recuadros puestos.
    ///
    /// Se crea al pedirlo y no en el alta de la persona porque hay gente dada de alta desde antes
    /// de que el panel existiera: un proceso de alta sólo cubriría a quien entre a partir de
    /// mañana, y el resto vería una pantalla vacía sin saber por qué.
    /// </summary>
    [Fact]
    public async Task Mi_panel_se_crea_solo_la_primera_vez_y_trae_recuadros()
    {
        var cliente = await AutenticarAsync();

        var panel = await MiPanelAsync(cliente);

        panel.GetProperty("esMio").GetBoolean().Should().BeTrue();
        panel.GetProperty("widgets").EnumerateArray().Should().NotBeEmpty(
            "un panel de inicio vacío no le dice nada a quien entra por primera vez");
    }

    /// <summary>Pedirlo dos veces devuelve el mismo, no crea uno nuevo cada vez.</summary>
    [Fact]
    public async Task Pedir_mi_panel_dos_veces_devuelve_el_mismo()
    {
        var cliente = await AutenticarAsync();

        var primera = await MiPanelAsync(cliente);
        var segunda = await MiPanelAsync(cliente);

        segunda.GetProperty("id").GetGuid().Should().Be(primera.GetProperty("id").GetGuid());
    }

    /// <summary>
    /// Los recuadros caben en la rejilla.
    ///
    /// Uno que empiece en la columna 10 y mida 4 se sale, y en pantalla eso se ve como un recuadro
    /// cortado o bajado de fila según el navegador. Se comprueba sobre los de partida porque son
    /// los que ve todo el mundo el primer día.
    /// </summary>
    [Fact]
    public async Task Los_recuadros_de_partida_caben_en_la_rejilla()
    {
        var cliente = await AutenticarAsync();
        var panel = await MiPanelAsync(cliente);

        foreach (var widget in panel.GetProperty("widgets").EnumerateArray())
        {
            var x = widget.GetProperty("x").GetInt32();
            var ancho = widget.GetProperty("ancho").GetInt32();

            (x + ancho).Should().BeLessThanOrEqualTo(12,
                $"el recuadro «{widget.GetProperty("titulo")}» se sale de las 12 columnas");
        }
    }

    #endregion

    #region Los datos

    /// <summary>
    /// <b>La prueba que importa: los recuadros de partida enseñan datos de verdad.</b>
    ///
    /// Ninguno puede venir con error, y al menos uno tiene que traer filas. Un panel de inicio
    /// donde todos los recuadros están vacíos o rotos es exactamente lo que el plan prohibía:
    /// «sólo se ofrece de serie lo que los datos de hoy pueden responder».
    /// </summary>
    [Fact]
    public async Task Los_recuadros_de_partida_traen_datos_y_ninguno_falla()
    {
        var cliente = await AutenticarAsync();
        var panel = await MiPanelAsync(cliente);
        var panelId = panel.GetProperty("id").GetGuid();

        var datos = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/dashboards/{panelId}/datos");
        var recuadros = datos.EnumerateArray().ToList();

        recuadros.Should().NotBeEmpty();

        var rotos = recuadros
            .Where(r => r.GetProperty("error").ValueKind != JsonValueKind.Null)
            .Select(r => $"{r.GetProperty("titulo").GetString()}: {r.GetProperty("error").GetString()}")
            .ToList();

        rotos.Should().BeEmpty("los recuadros de partida tienen que funcionar el primer día");

        recuadros.Should().Contain(
            r => r.GetProperty("filas").EnumerateArray().Any(),
            "al menos uno tiene que traer datos; si todos salen vacíos, el panel no dice nada");
    }

    /// <summary>
    /// Cada recuadro trae su forma y sus columnas: lo que la pantalla necesita para pintarlo.
    /// </summary>
    [Fact]
    public async Task Cada_recuadro_dice_como_pintarse()
    {
        var cliente = await AutenticarAsync();
        var panel = await MiPanelAsync(cliente);
        var panelId = panel.GetProperty("id").GetGuid();

        var datos = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/dashboards/{panelId}/datos");

        foreach (var recuadro in datos.EnumerateArray())
        {
            recuadro.GetProperty("forma").GetString().Should().NotBeNullOrWhiteSpace();
            recuadro.GetProperty("titulo").GetString().Should().NotBeNullOrWhiteSpace();
            recuadro.GetProperty("columnas").EnumerateArray().Should().NotBeEmpty();
        }
    }

    /// <summary>
    /// Un recuadro roto apaga su recuadro y deja los demás.
    ///
    /// Se provoca añadiendo un informe a medida **sin configurar**. Si el fallo tumbara la
    /// petición entera, un solo informe mal configurado dejaría la pantalla de inicio en blanco,
    /// y quien lo viera no sabría cuál de los seis es el culpable.
    /// </summary>
    [Fact]
    public async Task Un_recuadro_roto_no_tumba_el_panel_entero()
    {
        var cliente = await AutenticarAsync();
        var panel = await MiPanelAsync(cliente);
        var panelId = panel.GetProperty("id").GetGuid();

        var informe = await cliente.PostAsJsonAsync("/api/v1/reports", new
        {
            Name = $"Sin configurar {Guid.NewGuid():N}"[..30],
            Type = "Custom",
            Format = "Csv"
        });

        var informeId = (await informe.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var anadido = await cliente.PostAsJsonAsync($"/api/v1/dashboards/{panelId}/widgets",
            new { ReportId = informeId, Forma = "barras" });

        anadido.StatusCode.Should().Be(HttpStatusCode.OK, await anadido.Content.ReadAsStringAsync());
        var widgetId = (await anadido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        try
        {
            var datos = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/dashboards/{panelId}/datos");
            var recuadros = datos.EnumerateArray().ToList();

            var roto = recuadros.Single(r => r.GetProperty("widgetId").GetGuid() == widgetId);
            roto.GetProperty("error").GetString().Should().Contain("constructor",
                "el recuadro roto explica qué hacer, no sólo que falló");

            recuadros.Where(r => r.GetProperty("widgetId").GetGuid() != widgetId)
                .Should().OnlyContain(r => r.GetProperty("error").ValueKind == JsonValueKind.Null,
                    "los demás siguen funcionando");
        }
        finally
        {
            await cliente.DeleteAsync($"/api/v1/dashboards/{panelId}/widgets/{widgetId}");
        }
    }

    #endregion

    #region Colocar

    [Fact]
    public async Task La_disposicion_se_guarda_y_se_relee()
    {
        var cliente = await AutenticarAsync();
        var panel = await MiPanelAsync(cliente);
        var panelId = panel.GetProperty("id").GetGuid();

        var widgets = panel.GetProperty("widgets").EnumerateArray()
            .Select(w => new
            {
                id = w.GetProperty("id").GetGuid(),
                reportId = w.GetProperty("reportId").GetGuid(),
                x = 0,
                y = w.GetProperty("y").GetInt32(),
                ancho = 12,
                alto = 3,
                forma = w.GetProperty("forma").GetString(),
                titulo = w.GetProperty("titulo").GetString()
            })
            .ToList();

        var guardado = await cliente.PutAsJsonAsync(
            $"/api/v1/dashboards/{panelId}/disposicion", new { Widgets = widgets });

        guardado.StatusCode.Should().Be(HttpStatusCode.NoContent, await guardado.Content.ReadAsStringAsync());

        // Se relee en otra petición: la lección del PATCH que respondía 200 sin guardar.
        var despues = await MiPanelAsync(cliente);

        despues.GetProperty("widgets").EnumerateArray()
            .Should().OnlyContain(w => w.GetProperty("ancho").GetInt32() == 12);
    }

    /// <summary>
    /// Un recuadro que se sale de la rejilla se rechaza **al guardar**.
    ///
    /// Guardarlo y dejar que la pantalla se apañe significa que el panel se rompe al abrirlo, y
    /// para entonces quien lo movió ya ha cerrado.
    /// </summary>
    [Fact]
    public async Task Un_recuadro_fuera_de_la_rejilla_se_rechaza()
    {
        var cliente = await AutenticarAsync();
        var panel = await MiPanelAsync(cliente);
        var panelId = panel.GetProperty("id").GetGuid();

        var uno = panel.GetProperty("widgets").EnumerateArray().First();

        var respuesta = await cliente.PutAsJsonAsync($"/api/v1/dashboards/{panelId}/disposicion", new
        {
            Widgets = new[]
            {
                new
                {
                    id = uno.GetProperty("id").GetGuid(),
                    reportId = uno.GetProperty("reportId").GetGuid(),
                    x = 10, y = 0, ancho = 6, alto = 4,
                    forma = "barras", titulo = "Se sale"
                }
            }
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("12 columnas");
    }

    [Fact]
    public async Task Un_recuadro_se_puede_anadir_y_quitar()
    {
        var cliente = await AutenticarAsync();
        var panel = await MiPanelAsync(cliente);
        var panelId = panel.GetProperty("id").GetGuid();

        var cuantosAntes = panel.GetProperty("widgets").EnumerateArray().Count();

        var informe = await cliente.PostAsJsonAsync("/api/v1/reports", new
        {
            Name = $"Para el panel {Guid.NewGuid():N}"[..30],
            Type = "KpiSummary",
            Format = "Csv"
        });
        var informeId = (await informe.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var anadido = await cliente.PostAsJsonAsync($"/api/v1/dashboards/{panelId}/widgets",
            new { ReportId = informeId, Forma = (string?)null });

        var widgetId = (await anadido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await MiPanelAsync(cliente)).GetProperty("widgets").EnumerateArray()
            .Should().HaveCount(cuantosAntes + 1);

        (await cliente.DeleteAsync($"/api/v1/dashboards/{panelId}/widgets/{widgetId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await MiPanelAsync(cliente)).GetProperty("widgets").EnumerateArray()
            .Should().HaveCount(cuantosAntes);
    }

    /// <summary>
    /// Quitar un recuadro no borra su informe.
    ///
    /// Sacar algo del panel es un gesto de colocación; borrar el informe sería destruir trabajo
    /// por un gesto que no lo pedía.
    /// </summary>
    [Fact]
    public async Task Quitar_un_recuadro_no_borra_el_informe()
    {
        var cliente = await AutenticarAsync();
        var panel = await MiPanelAsync(cliente);
        var panelId = panel.GetProperty("id").GetGuid();

        var uno = panel.GetProperty("widgets").EnumerateArray().First();
        var widgetId = uno.GetProperty("id").GetGuid();
        var informeId = uno.GetProperty("reportId").GetGuid();

        await cliente.DeleteAsync($"/api/v1/dashboards/{panelId}/widgets/{widgetId}");

        var informe = await cliente.GetAsync($"/api/v1/reports/{informeId}");
        informe.StatusCode.Should().Be(HttpStatusCode.OK, "el informe sigue en su lista");

        // Se devuelve al panel para no dejar el arranque cambiado a las demás pruebas.
        await cliente.PostAsJsonAsync($"/api/v1/dashboards/{panelId}/widgets",
            new { ReportId = informeId, Forma = uno.GetProperty("forma").GetString() });
    }

    #endregion

    [Fact]
    public async Task Sin_autenticar_no_hay_panel()
    {
        var anonimo = factory.CreateClient();

        (await anonimo.GetAsync("/api/v1/dashboards/mio")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }
}
