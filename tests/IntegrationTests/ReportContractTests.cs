using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Que la API acepte todo lo que el formulario ofrece.
///
/// Existen por un fallo que llegó hasta la pantalla: el desplegable de «Solicitar informe»
/// ofrecía cuatro tipos y **dos de ellos no existían en el backend**. Pedir «Distribución de
/// Tareas» o «Resumen de KPIs» devolvía 400 con el mensaje «Invalid report type or format»,
/// que el frontend enseñaba como «Error al solicitar el reporte». La mitad del formulario no
/// funcionaba y ninguna prueba lo veía.
///
/// La forma de que no vuelva a pasar no es acordarse: es **leer las opciones del propio
/// desplegable** y comprobar que cada una se acepta. Si alguien añade una opción a la pantalla
/// sin darla de alta aquí, esto se pone rojo antes de que nadie la pulse.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReportContractTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    /// <summary>La plantilla del formulario, relativa a la raíz del repositorio.</summary>
    private const string Template = "web/src/app/features/reports/report-create-modal.component.html";

    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static DirectoryInfo RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CrmSaaS.sln")))
            dir = dir.Parent;

        dir.Should().NotBeNull("las pruebas se ejecutan dentro del repositorio");
        return dir!;
    }

    /// <summary>
    /// Los `value` de un `select` de la plantilla.
    ///
    /// Se lee el HTML en lugar de repetir la lista aquí a propósito: una copia se desincroniza
    /// igual que se desincronizó la del backend, y entonces la prueba pasaría comprobando algo
    /// que ya nadie usa.
    /// </summary>
    private static IReadOnlyList<string> OptionsOf(string selectId)
    {
        var path = Path.Combine(RepositoryRoot().FullName, Template.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"la plantilla debería estar en {Template}");

        var html = File.ReadAllText(path);

        var select = Regex.Match(html, $@"<select[^>]*id=""{Regex.Escape(selectId)}""[^>]*>(.*?)</select>", RegexOptions.Singleline);
        select.Success.Should().BeTrue($"no se encontró el <select id=\"{selectId}\"> en la plantilla");

        var options = Regex.Matches(select.Groups[1].Value, @"<option[^>]*value=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .Where(v => v.Length > 0)
            .ToList();

        options.Should().NotBeEmpty();
        return options;
    }

    public static TheoryData<string> TypesTheScreenOffers()
    {
        var data = new TheoryData<string>();
        foreach (var type in OptionsOf("report-type")) data.Add(type);
        return data;
    }

    public static TheoryData<string> FormatsTheScreenOffers()
    {
        var data = new TheoryData<string>();
        foreach (var format in OptionsOf("report-format")) data.Add(format);
        return data;
    }

    [Theory]
    [MemberData(nameof(TypesTheScreenOffers))]
    public async Task The_api_accepts_every_type_the_form_offers(string type)
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe de prueba",
            type = type,
            format = "Csv",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"«{type}» se puede elegir en el formulario, así que la API tiene que aceptarlo. "
            + $"Respuesta: {await response.Content.ReadAsStringAsync()}");
    }

    [Theory]
    [MemberData(nameof(FormatsTheScreenOffers))]
    public async Task The_api_accepts_every_format_the_form_offers(string format)
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe de prueba",
            type = "Custom",
            format = format,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"«{format}» se puede elegir en el formulario. Respuesta: {await response.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// Y cuando de verdad no existe, que el mensaje diga qué arreglar.
    ///
    /// El anterior era «Invalid report type or format» para los dos casos: ni decía cuál de los
    /// dos, ni cuáles valían. Un error que no dice qué cambiar cuesta lo mismo que no darlo.
    /// </summary>
    [Fact]
    public async Task An_unknown_type_returns_an_error_listing_the_valid_ones()
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe de prueba",
            type = "EsteTipoNoExiste",
            format = "Csv",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("EsteTipoNoExiste", "hay que decir qué se pidió");
        text.Should().Contain("ProjectProgress", "y qué se podría haber pedido");
    }

    /// <summary>
    /// El informe ya no finge tener un estado de generación, y esta prueba lo vigila.
    ///
    /// Su versión anterior comprobaba lo contrario: que tras llamar a `/generate` el informe se
    /// leyera como generado y con URL. Y pasaba —el mapeo del DTO se había arreglado para que
    /// esos campos viajaran— pero **lo que viajaba era mentira**: la URL la fabricaba
    /// `MarkAsGenerated` a mano y no apuntaba a ningún fichero.
    ///
    /// Los cuatro campos se han quitado del informe. El estado vive ahora en las exportaciones,
    /// una por petición y por formato, porque un informe se exporta muchas veces y cuatro campos
    /// sueltos sólo saben contar la última.
    ///
    /// Se comprueba **volviendo a preguntar** en otra petición, no mirando lo que devolvió el
    /// POST. Es la misma lección del PATCH que respondía 200 sin guardar.
    /// </summary>
    [Fact]
    public async Task The_report_has_no_made_up_generation_status()
    {
        var client = await AuthenticateAsync();

        var creation = await client.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe que se genera",
            type = "TaskBreakdown",
            format = "Csv",
        });
        creation.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var report = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{id}");

        foreach (var field in new[] { "isGenerated", "generatedFileUrl", "generatedAt", "errorMessage" })
        {
            report.TryGetProperty(field, out _).Should().BeFalse(
                $"«{field}» describía una sola generación por informe y además guardaba una URL "
                + "que no llevaba a ningún fichero. Ahora eso lo cuentan las exportaciones");
        }

        // Y lo que sí tiene que llegar, llega: el mapeo del DTO sigue sin dejarse campos por el
        // camino, que es la preocupación original de esta prueba.
        report.GetProperty("name").GetString().Should().Be("Informe que se genera");
        report.GetProperty("type").GetString().Should().Be("TaskBreakdown");
        report.GetProperty("format").GetString().Should().Be("Csv");
    }

    /// <summary>
    /// El listado usa el mismo mapeo y se consulta por otro camino, así que también se mira.
    ///
    /// Antes comprobaba que el listado trajera el estado de generación; ahora comprueba que la
    /// exportación se vea desde el informe. Es la misma preocupación —que el estado llegue hasta
    /// la pantalla— sobre el sitio donde el estado es cierto.
    /// </summary>
    [Fact]
    public async Task A_report_shows_its_exports()
    {
        var client = await AuthenticateAsync();

        var creation = await client.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe en el listado",
            type = "KpiSummary",
            format = "Excel",
        });
        var id = (await creation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var withNothing = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{id}/exports");
        withNothing.EnumerateArray().Should().BeEmpty("recién creado no se ha exportado nunca");

        await client.PostAsync($"/api/v1/reports/{id}/export?format=Excel", null);

        var withOne = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{id}/exports");

        var own = withOne.EnumerateArray().Should().ContainSingle().Subject;
        own.GetProperty("format").GetString().Should().Be("Excel");
        own.GetProperty("status").GetString().Should().BeOneOf("Pending", "Generating", "Ready");
    }

    [Fact]
    public async Task An_unknown_format_points_at_the_format_not_the_type()
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe de prueba",
            type = "Custom",
            format = "Papiro",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("Papiro");
        text.Should().Contain("formato", "el mensaje anterior culpaba al tipo y al formato a la vez");
    }
}
