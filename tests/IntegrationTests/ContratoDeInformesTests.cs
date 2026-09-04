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
    public async Task El_informe_no_lleva_estado_de_generacion_inventado()
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

        var informe = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{id}");

        foreach (var campo in new[] { "isGenerated", "generatedFileUrl", "generatedAt", "errorMessage" })
        {
            informe.TryGetProperty(campo, out _).Should().BeFalse(
                $"«{campo}» describía una sola generación por informe y además guardaba una URL "
                + "que no llevaba a ningún fichero. Ahora eso lo cuentan las exportaciones");
        }

        // Y lo que sí tiene que llegar, llega: el mapeo del DTO sigue sin dejarse campos por el
        // camino, que es la preocupación original de esta prueba.
        informe.GetProperty("name").GetString().Should().Be("Informe que se genera");
        informe.GetProperty("type").GetString().Should().Be("TaskBreakdown");
        informe.GetProperty("format").GetString().Should().Be("Csv");
    }

    /// <summary>
    /// El listado usa el mismo mapeo y se consulta por otro camino, así que también se mira.
    ///
    /// Antes comprobaba que el listado trajera el estado de generación; ahora comprueba que la
    /// exportación se vea desde el informe. Es la misma preocupación —que el estado llegue hasta
    /// la pantalla— sobre el sitio donde el estado es cierto.
    /// </summary>
    [Fact]
    public async Task Desde_el_informe_se_ven_sus_exportaciones()
    {
        var cliente = await AutenticarAsync();

        var alta = await cliente.PostAsJsonAsync("/api/v1/reports", new
        {
            name = "Informe en el listado",
            type = "KpiSummary",
            format = "Excel",
        });
        var id = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var sinNada = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{id}/exportaciones");
        sinNada.EnumerateArray().Should().BeEmpty("recién creado no se ha exportado nunca");

        await cliente.PostAsync($"/api/v1/reports/{id}/exportar?format=Excel", null);

        var conUna = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{id}/exportaciones");

        var suya = conUna.EnumerateArray().Should().ContainSingle().Subject;
        suya.GetProperty("formato").GetString().Should().Be("Excel");
        suya.GetProperty("estado").GetString().Should().BeOneOf("Pendiente", "Generando", "Lista");
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
