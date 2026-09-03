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
public sealed class ContratoDeInformesTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    /// <summary>La plantilla del formulario, relativa a la raíz del repositorio.</summary>
    private const string Plantilla = "web/src/app/features/reports/report-create-modal.component.html";

    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static DirectoryInfo RaizDelRepositorio()
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
    private static IReadOnlyList<string> OpcionesDe(string idDelSelect)
    {
        var ruta = Path.Combine(RaizDelRepositorio().FullName, Plantilla.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(ruta).Should().BeTrue($"la plantilla debería estar en {Plantilla}");

        var html = File.ReadAllText(ruta);

        var select = Regex.Match(html, $@"<select[^>]*id=""{Regex.Escape(idDelSelect)}""[^>]*>(.*?)</select>", RegexOptions.Singleline);
        select.Success.Should().BeTrue($"no se encontró el <select id=\"{idDelSelect}\"> en la plantilla");

        var opciones = Regex.Matches(select.Groups[1].Value, @"<option[^>]*value=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .Where(v => v.Length > 0)
            .ToList();

        opciones.Should().NotBeEmpty();
        return opciones;
    }

    public static TheoryData<string> TiposQueOfreceLaPantalla()
    {
        var datos = new TheoryData<string>();
        foreach (var tipo in OpcionesDe("report-type")) datos.Add(tipo);
        return datos;
    }

    public static TheoryData<string> FormatosQueOfreceLaPantalla()
    {
        var datos = new TheoryData<string>();
        foreach (var formato in OpcionesDe("report-format")) datos.Add(formato);
        return datos;
    }

    [Theory]
    [MemberData(nameof(TiposQueOfreceLaPantalla))]
    public async Task La_api_acepta_todos_los_tipos_que_ofrece_el_formulario(string tipo)
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe de prueba",
            type = tipo,
            format = "Csv",
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created,
            $"«{tipo}» se puede elegir en el formulario, así que la API tiene que aceptarlo. "
            + $"Respuesta: {await respuesta.Content.ReadAsStringAsync()}");
    }

    [Theory]
    [MemberData(nameof(FormatosQueOfreceLaPantalla))]
    public async Task La_api_acepta_todos_los_formatos_que_ofrece_el_formulario(string formato)
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe de prueba",
            type = "Custom",
            format = formato,
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created,
            $"«{formato}» se puede elegir en el formulario. Respuesta: {await respuesta.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// Y cuando de verdad no existe, que el mensaje diga qué arreglar.
    ///
    /// El anterior era «Invalid report type or format» para los dos casos: ni decía cuál de los
    /// dos, ni cuáles valían. Un error que no dice qué cambiar cuesta lo mismo que no darlo.
    /// </summary>
    [Fact]
    public async Task Un_tipo_inexistente_da_un_error_que_dice_cuales_valen()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe de prueba",
            type = "EsteTipoNoExiste",
            format = "Csv",
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var texto = await respuesta.Content.ReadAsStringAsync();
        texto.Should().Contain("EsteTipoNoExiste", "hay que decir qué se pidió");
        texto.Should().Contain("ProjectProgress", "y qué se podría haber pedido");
    }

    /// <summary>
    /// El estado de generación tiene que llegar hasta quien lo pide.
    ///
    /// No llegaba. `ReportDto.FromEntity` copiaba siete campos y se dejaba cuatro, así que un
    /// informe generado salía por la API como no generado y sin URL: la pantalla no podía
    /// enseñarlo como listo ni ofrecer la descarga por mucho que la base dijera lo contrario.
    ///
    /// Se comprueba **volviendo a preguntar** en otra petición, no mirando lo que devolvió el
    /// POST. Es la misma lección del PATCH que respondía 200 sin guardar.
    /// </summary>
    [Fact]
    public async Task Un_informe_generado_se_lee_como_generado()
    {
        var cliente = await AutenticarAsync();

        var alta = await cliente.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe que se genera",
            type = "TaskBreakdown",
            format = "Csv",
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var reciennacido = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{id}");
        reciennacido.GetProperty("isGenerated").GetBoolean().Should().BeFalse();
        reciennacido.GetProperty("generatedAt").ValueKind.Should().Be(JsonValueKind.Null,
            "un informe recién pedido no se ha generado, así que no puede traer fecha de generación; "
            + "el DTO tenía DateTime.Now por defecto e inventaba una distinta en cada llamada");

        var generacion = await cliente.PostAsync($"/api/v1/reports/{id}/generate?format=Csv", null);
        generacion.StatusCode.Should().Be(HttpStatusCode.OK);

        var despues = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{id}");

        despues.GetProperty("isGenerated").GetBoolean().Should().BeTrue();
        despues.GetProperty("generatedFileUrl").GetString().Should().NotBeNullOrWhiteSpace();
        despues.GetProperty("generatedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    /// <summary>El listado usa el mismo mapeo, y se consulta por otro camino: también se mira.</summary>
    [Fact]
    public async Task El_listado_tambien_trae_el_estado_de_generacion()
    {
        var cliente = await AutenticarAsync();

        var alta = await cliente.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe en el listado",
            type = "KpiSummary",
            format = "Excel",
        });
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await cliente.PostAsync($"/api/v1/reports/{id}/generate?format=Excel", null);

        var listado = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/reports?pageSize=100");

        var mio = listado.GetProperty("items").EnumerateArray()
            .Single(r => r.GetProperty("id").GetGuid() == id);

        mio.GetProperty("isGenerated").GetBoolean().Should().BeTrue();
        mio.GetProperty("generatedFileUrl").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Un_formato_inexistente_senala_al_formato_y_no_al_tipo()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe de prueba",
            type = "Custom",
            format = "Papiro",
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var texto = await respuesta.Content.ReadAsStringAsync();
        texto.Should().Contain("Papiro");
        texto.Should().Contain("formato", "el mensaje anterior culpaba al tipo y al formato a la vez");
    }
}
