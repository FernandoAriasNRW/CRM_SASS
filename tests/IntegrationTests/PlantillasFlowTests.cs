using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El contador que ordena la galería de plantillas.
///
/// La galería enseña cuatro plantillas de las que haya, y las cuatro que enseña son las más
/// usadas. Que sean «las más usadas» depende de un contador que sube al crear; si no subiera, la
/// galería seguiría pintándose igual de bien y siempre con las mismas cuatro. Nadie lo notaría
/// mirando la pantalla, que es exactamente el tipo de fallo que este proyecto lleva persiguiendo.
///
/// Por eso lo que se comprueba es <b>el efecto</b>: crear desde una plantilla cambia lo que
/// devuelve el contador, y la que más se usa queda la primera.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PlantillasFlowTests(CrmApiFactory factory)
{
    private async Task<HttpClient> AutenticarAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static async Task<Dictionary<string, int>> UsosAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetAsync("/api/v1/docs/templates/usage");
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var usos = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return usos.EnumerateArray().ToDictionary(
            u => u.GetProperty("key").GetString()!,
            u => u.GetProperty("count").GetInt32());
    }

    private static async Task CrearDesdeAsync(HttpClient cliente, string clave)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/docs/from-template", new { TemplateKey = clave });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());
    }

    /// <summary>Crear desde una plantilla suma uno, y sólo a la suya.</summary>
    [Fact]
    public async Task Crear_desde_una_plantilla_sube_su_contador()
    {
        var cliente = await AutenticarAsync();

        var antes = await UsosAsync(cliente);
        var wikiAntes = antes.GetValueOrDefault("wiki");
        var actaAntes = antes.GetValueOrDefault("meeting-notes");

        await CrearDesdeAsync(cliente, "wiki");

        var despues = await UsosAsync(cliente);
        despues.GetValueOrDefault("wiki").Should().Be(wikiAntes + 1);
        despues.GetValueOrDefault("meeting-notes").Should().Be(actaAntes,
            "sólo sube la plantilla que se ha usado");
    }

    /// <summary>
    /// La más usada sale la primera, que es lo único por lo que existe el contador.
    ///
    /// Se usa una plantilla lo bastante como para adelantar a la que fuera primera, en vez de dar
    /// por hecho que la base empieza en cero: la colección comparte inquilino y otra prueba puede
    /// haber creado documentos antes.
    /// </summary>
    [Fact]
    public async Task La_mas_usada_queda_la_primera()
    {
        var cliente = await AutenticarAsync();

        var antes = await UsosAsync(cliente);
        var maximo = antes.Count == 0 ? 0 : antes.Values.Max();

        for (var i = 0; i <= maximo; i++)
            await CrearDesdeAsync(cliente, "client-onboarding");

        var respuesta = await cliente.GetAsync("/api/v1/docs/templates/usage");
        var usos = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

        usos.EnumerateArray().First().GetProperty("key").GetString()
            .Should().Be("client-onboarding", "el listado llega ordenado de más usada a menos");
    }

    private static async Task<(string titulo, string contenido)> CrearYLeerAsync(
        HttpClient cliente, string clave, string? idioma)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/docs/from-template",
            new { TemplateKey = clave, Language = idioma });
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        var id = Guid.Parse((await respuesta.Content.ReadAsStringAsync()).Trim('"'));

        var documentos = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/docs/");
        var titulo = documentos.EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == id).GetProperty("title").GetString()!;

        var paginas = await cliente.GetFromJsonAsync<JsonElement>($"/api/v1/docs/{id}/pages");
        var contenido = paginas.EnumerateArray().First().GetProperty("content").GetString()!;

        return (titulo, contenido);
    }

    /// <summary>
    /// El contenido de la plantilla sale en el idioma de la aplicación.
    ///
    /// Estaba escrito sólo en inglés dentro del handler: la galería anunciaba «Acta de reunión» y al
    /// pulsarla se creaba «Meeting Notes» con «Action Items» dentro.
    /// </summary>
    [Fact]
    public async Task El_contenido_sale_en_el_idioma_pedido()
    {
        var cliente = await AutenticarAsync();

        var (tituloEs, contenidoEs) = await CrearYLeerAsync(cliente, "meeting-notes", "es");
        tituloEs.Should().Be("Acta de reunión");
        contenidoEs.Should().Contain("Orden del día").And.NotContain("Agenda");

        var (tituloEn, contenidoEn) = await CrearYLeerAsync(cliente, "meeting-notes", "en");
        tituloEn.Should().Be("Meeting notes");
        contenidoEn.Should().Contain("Agenda").And.NotContain("Orden del día");
    }

    /// <summary>Sin idioma, español: es el idioma de origen de la aplicación.</summary>
    [Fact]
    public async Task Sin_idioma_sale_en_espanol()
    {
        var cliente = await AutenticarAsync();

        var (titulo, _) = await CrearYLeerAsync(cliente, "wiki", null);

        titulo.Should().Be("Wiki del equipo");
    }

    /// <summary>
    /// Las casillas son listas de tareas de verdad, no corchetes escritos como texto.
    ///
    /// Antes la plantilla escribía «[ ] Kickoff» dentro de una viñeta normal: se veían los
    /// corchetes y no se podían marcar.
    /// </summary>
    [Fact]
    public async Task Las_casillas_se_pueden_marcar()
    {
        var cliente = await AutenticarAsync();

        var (_, contenido) = await CrearYLeerAsync(cliente, "project-overview", "es");

        contenido.Should().Contain("data-type=\"taskItem\"").And.NotContain("[ ]");
    }

    /// <summary>
    /// Una plantilla que no existe se rechaza.
    ///
    /// Antes caía en un <c>default</c> que creaba un «Untitled Document» y le contaba un uso a la
    /// plantilla inexistente: respondía bien haciendo otra cosa.
    /// </summary>
    [Fact]
    public async Task Una_plantilla_que_no_existe_se_rechaza_y_no_cuenta()
    {
        var cliente = await AutenticarAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/v1/docs/from-template",
            new { TemplateKey = "no-existe" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var usos = await UsosAsync(cliente);
        usos.Should().NotContainKey("no-existe", "una plantilla inexistente no puede sumar usos");
    }
}
