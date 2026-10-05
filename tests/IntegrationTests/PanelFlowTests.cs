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

    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<JsonElement> MyDashboardAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/dashboards/mine");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
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
    public async Task My_dashboard_is_created_once_with_widgets()
    {
        var client = await AuthenticateAsync();

        var panel = await MyDashboardAsync(client);

        panel.GetProperty("isMine").GetBoolean().Should().BeTrue();
        panel.GetProperty("widgets").EnumerateArray().Should().NotBeEmpty(
            "un panel de inicio vacío no le dice nada a quien entra por primera vez");
    }

    /// <summary>Pedirlo dos veces devuelve el mismo, no crea uno nuevo cada vez.</summary>
    [Fact]
    public async Task Asking_for_my_dashboard_twice_returns_the_same()
    {
        var client = await AuthenticateAsync();

        var first = await MyDashboardAsync(client);
        var second = await MyDashboardAsync(client);

        second.GetProperty("id").GetGuid().Should().Be(first.GetProperty("id").GetGuid());
    }

    /// <summary>
    /// Los recuadros caben en la rejilla.
    ///
    /// Uno que empiece en la columna 10 y mida 4 se sale, y en pantalla eso se ve como un recuadro
    /// cortado o bajado de fila según el navegador. Se comprueba sobre los de partida porque son
    /// los que ve todo el mundo el primer día.
    /// </summary>
    [Fact]
    public async Task Starter_widgets_fit_the_grid()
    {
        var client = await AuthenticateAsync();
        var panel = await MyDashboardAsync(client);

        foreach (var widget in panel.GetProperty("widgets").EnumerateArray())
        {
            var x = widget.GetProperty("x").GetInt32();
            var width = widget.GetProperty("width").GetInt32();

            (x + width).Should().BeLessThanOrEqualTo(12,
                $"el recuadro «{widget.GetProperty("title")}» se sale de las 12 columnas");
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
    public async Task Starter_widgets_bring_data_and_none_fails()
    {
        var client = await AuthenticateAsync();
        var panel = await MyDashboardAsync(client);
        var panelId = panel.GetProperty("id").GetGuid();

        var data = await client.GetFromJsonAsync<JsonElement>($"/api/v1/dashboards/{panelId}/data");
        var widgets = data.EnumerateArray().ToList();

        widgets.Should().NotBeEmpty();

        var broken = widgets
            .Where(r => r.GetProperty("error").ValueKind != JsonValueKind.Null)
            .Select(r => $"{r.GetProperty("title").GetString()}: {r.GetProperty("error").GetString()}")
            .ToList();

        broken.Should().BeEmpty("los recuadros de partida tienen que funcionar el primer día");

        widgets.Should().Contain(
            r => r.GetProperty("rows").EnumerateArray().Any(),
            "al menos uno tiene que traer datos; si todos salen vacíos, el panel no dice nada");
    }

    /// <summary>
    /// Cada recuadro trae su forma y sus columnas: lo que la pantalla necesita para pintarlo.
    /// </summary>
    [Fact]
    public async Task Each_widget_says_how_to_render()
    {
        var client = await AuthenticateAsync();
        var panel = await MyDashboardAsync(client);
        var panelId = panel.GetProperty("id").GetGuid();

        var data = await client.GetFromJsonAsync<JsonElement>($"/api/v1/dashboards/{panelId}/data");

        foreach (var widget in data.EnumerateArray())
        {
            widget.GetProperty("visualization").GetString().Should().NotBeNullOrWhiteSpace();
            widget.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
            widget.GetProperty("columns").EnumerateArray().Should().NotBeEmpty();
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
    public async Task A_broken_widget_does_not_take_down_the_dashboard()
    {
        var client = await AuthenticateAsync();
        var panel = await MyDashboardAsync(client);
        var panelId = panel.GetProperty("id").GetGuid();

        var report = await client.PostAsJsonAsync("/api/v1/reports", new
        {
            Name = $"Sin configurar {Guid.NewGuid():N}"[..30],
            Type = "Custom",
            Format = "Csv"
        });

        var reportId = (await report.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var anadido = await client.PostAsJsonAsync($"/api/v1/dashboards/{panelId}/widgets",
            new { ReportId = reportId, Visualization = "bar" });

        anadido.StatusCode.Should().Be(HttpStatusCode.OK, await anadido.Content.ReadAsStringAsync());
        var widgetId = (await anadido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        try
        {
            var data = await client.GetFromJsonAsync<JsonElement>($"/api/v1/dashboards/{panelId}/data");
            var widgets = data.EnumerateArray().ToList();

            var broken = widgets.Single(r => r.GetProperty("widgetId").GetGuid() == widgetId);
            broken.GetProperty("error").GetString().Should().Contain("constructor",
                "el recuadro roto explica qué hacer, no sólo que falló");

            widgets.Where(r => r.GetProperty("widgetId").GetGuid() != widgetId)
                .Should().OnlyContain(r => r.GetProperty("error").ValueKind == JsonValueKind.Null,
                    "los demás siguen funcionando");
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/dashboards/{panelId}/widgets/{widgetId}");
        }
    }

    #endregion

    #region Colocar

    [Fact]
    public async Task The_layout_is_saved_and_reread()
    {
        var client = await AuthenticateAsync();
        var panel = await MyDashboardAsync(client);
        var panelId = panel.GetProperty("id").GetGuid();

        var widgets = panel.GetProperty("widgets").EnumerateArray()
            .Select(w => new
            {
                id = w.GetProperty("id").GetGuid(),
                reportId = w.GetProperty("reportId").GetGuid(),
                x = 0,
                y = w.GetProperty("y").GetInt32(),
                width = 12,
                height = 3,
                visualization = w.GetProperty("visualization").GetString(),
                title = w.GetProperty("title").GetString()
            })
            .ToList();

        var saved = await client.PutAsJsonAsync(
            $"/api/v1/dashboards/{panelId}/layout", new { Widgets = widgets });

        saved.StatusCode.Should().Be(HttpStatusCode.NoContent, await saved.Content.ReadAsStringAsync());

        // Se relee en otra petición: la lección del PATCH que respondía 200 sin guardar.
        var after = await MyDashboardAsync(client);

        after.GetProperty("widgets").EnumerateArray()
            .Should().OnlyContain(w => w.GetProperty("width").GetInt32() == 12);
    }

    /// <summary>
    /// Un recuadro que se sale de la rejilla se rechaza **al guardar**.
    ///
    /// Guardarlo y dejar que la pantalla se apañe significa que el panel se rompe al abrirlo, y
    /// para entonces quien lo movió ya ha cerrado.
    /// </summary>
    [Fact]
    public async Task A_widget_outside_the_grid_is_rejected()
    {
        var client = await AuthenticateAsync();
        var panel = await MyDashboardAsync(client);
        var panelId = panel.GetProperty("id").GetGuid();

        var one = panel.GetProperty("widgets").EnumerateArray().First();

        var response = await client.PutAsJsonAsync($"/api/v1/dashboards/{panelId}/layout", new
        {
            Widgets = new[]
            {
                new
                {
                    id = one.GetProperty("id").GetGuid(),
                    reportId = one.GetProperty("reportId").GetGuid(),
                    x = 10, y = 0, width = 6, height = 4,
                    visualization = "bar", title = "Se sale"
                }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("12 columnas");
    }

    [Fact]
    public async Task A_widget_can_be_added_and_removed()
    {
        var client = await AuthenticateAsync();
        var panel = await MyDashboardAsync(client);
        var panelId = panel.GetProperty("id").GetGuid();

        var countBefore = panel.GetProperty("widgets").EnumerateArray().Count();

        var report = await client.PostAsJsonAsync("/api/v1/reports", new
        {
            Name = $"Para el panel {Guid.NewGuid():N}"[..30],
            Type = "KpiSummary",
            Format = "Csv"
        });
        var reportId = (await report.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var anadido = await client.PostAsJsonAsync($"/api/v1/dashboards/{panelId}/widgets",
            new { ReportId = reportId, Visualization = (string?)null });

        var widgetId = (await anadido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await MyDashboardAsync(client)).GetProperty("widgets").EnumerateArray()
            .Should().HaveCount(countBefore + 1);

        (await client.DeleteAsync($"/api/v1/dashboards/{panelId}/widgets/{widgetId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await MyDashboardAsync(client)).GetProperty("widgets").EnumerateArray()
            .Should().HaveCount(countBefore);
    }

    /// <summary>
    /// Quitar un recuadro no borra su informe.
    ///
    /// Sacar algo del panel es un gesto de colocación; borrar el informe sería destruir trabajo
    /// por un gesto que no lo pedía.
    /// </summary>
    [Fact]
    public async Task Removing_a_widget_keeps_the_report()
    {
        var client = await AuthenticateAsync();
        var panel = await MyDashboardAsync(client);
        var panelId = panel.GetProperty("id").GetGuid();

        var one = panel.GetProperty("widgets").EnumerateArray().First();
        var widgetId = one.GetProperty("id").GetGuid();
        var reportId = one.GetProperty("reportId").GetGuid();

        await client.DeleteAsync($"/api/v1/dashboards/{panelId}/widgets/{widgetId}");

        var report = await client.GetAsync($"/api/v1/reports/{reportId}");
        report.StatusCode.Should().Be(HttpStatusCode.OK, "el informe sigue en su lista");

        // Se devuelve al panel para no dejar el arranque cambiado a las demás pruebas.
        await client.PostAsJsonAsync($"/api/v1/dashboards/{panelId}/widgets",
            new { ReportId = reportId, Visualization = one.GetProperty("visualization").GetString() });
    }

    #endregion

    [Fact]
    public async Task Without_authentication_there_is_no_dashboard()
    {
        var anonymous = factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/dashboards/mine")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }
}
