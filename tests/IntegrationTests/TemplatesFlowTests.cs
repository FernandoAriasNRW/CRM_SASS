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
public sealed class TemplatesFlowTests(CrmApiFactory factory)
{
    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<Dictionary<string, int>> UsesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/docs/templates/usage");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var uses = await response.Content.ReadFromJsonAsync<JsonElement>();
        return uses.EnumerateArray().ToDictionary(
            u => u.GetProperty("key").GetString()!,
            u => u.GetProperty("count").GetInt32());
    }

    private static async Task CreateFromAsync(HttpClient client, string key)
    {
        var response = await client.PostAsJsonAsync("/api/v1/docs/from-template", new { TemplateKey = key });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Crear desde una plantilla suma uno, y sólo a la suya.</summary>
    [Fact]
    public async Task Creating_from_a_template_raises_its_counter()
    {
        var client = await AuthenticateAsync();

        var before = await UsesAsync(client);
        var wikiBefore = before.GetValueOrDefault("wiki");
        var minutesBefore = before.GetValueOrDefault("meeting-notes");

        await CreateFromAsync(client, "wiki");

        var after = await UsesAsync(client);
        after.GetValueOrDefault("wiki").Should().Be(wikiBefore + 1);
        after.GetValueOrDefault("meeting-notes").Should().Be(minutesBefore,
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
    public async Task The_most_used_comes_first()
    {
        var client = await AuthenticateAsync();

        var before = await UsesAsync(client);
        var max = before.Count == 0 ? 0 : before.Values.Max();

        for (var i = 0; i <= max; i++)
            await CreateFromAsync(client, "client-onboarding");

        var response = await client.GetAsync("/api/v1/docs/templates/usage");
        var uses = await response.Content.ReadFromJsonAsync<JsonElement>();

        uses.EnumerateArray().First().GetProperty("key").GetString()
            .Should().Be("client-onboarding", "el listado llega ordenado de más usada a menos");
    }

    private static async Task<(string title, string content)> CreateAndReadAsync(
        HttpClient client, string key, string? language)
    {
        var response = await client.PostAsJsonAsync("/api/v1/docs/from-template",
            new { TemplateKey = key, Language = language });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var id = Guid.Parse((await response.Content.ReadAsStringAsync()).Trim('"'));

        var documents = await client.GetFromJsonAsync<JsonElement>("/api/v1/docs/");
        var title = documents.EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == id).GetProperty("title").GetString()!;

        var pages = await client.GetFromJsonAsync<JsonElement>($"/api/v1/docs/{id}/pages");
        var content = pages.EnumerateArray().First().GetProperty("content").GetString()!;

        return (title, content);
    }

    /// <summary>
    /// El contenido de la plantilla sale en el idioma de la aplicación.
    ///
    /// Estaba escrito sólo en inglés dentro del handler: la galería anunciaba «Acta de reunión» y al
    /// pulsarla se creaba «Meeting Notes» con «Action Items» dentro.
    /// </summary>
    [Fact]
    public async Task Content_comes_in_the_requested_language()
    {
        var client = await AuthenticateAsync();

        var (titleEs, contentEs) = await CreateAndReadAsync(client, "meeting-notes", "es");
        titleEs.Should().Be("Acta de reunión");
        contentEs.Should().Contain("Orden del día").And.NotContain("Agenda");

        var (titleEn, contentEn) = await CreateAndReadAsync(client, "meeting-notes", "en");
        titleEn.Should().Be("Meeting notes");
        contentEn.Should().Contain("Agenda").And.NotContain("Orden del día");
    }

    /// <summary>Sin idioma, español: es el idioma de origen de la aplicación.</summary>
    [Fact]
    public async Task Without_language_it_comes_in_Spanish()
    {
        var client = await AuthenticateAsync();

        var (title, _) = await CreateAndReadAsync(client, "wiki", null);

        title.Should().Be("Wiki del equipo");
    }

    /// <summary>
    /// Las casillas son listas de tareas de verdad, no corchetes escritos como texto.
    ///
    /// Antes la plantilla escribía «[ ] Kickoff» dentro de una viñeta normal: se veían los
    /// corchetes y no se podían marcar.
    /// </summary>
    [Fact]
    public async Task Checkboxes_can_be_checked()
    {
        var client = await AuthenticateAsync();

        var (_, content) = await CreateAndReadAsync(client, "project-overview", "es");

        content.Should().Contain("data-type=\"taskItem\"").And.NotContain("[ ]");
    }

    /// <summary>
    /// Una plantilla que no existe se rechaza.
    ///
    /// Antes caía en un <c>default</c> que creaba un «Untitled Document» y le contaba un uso a la
    /// plantilla inexistente: respondía bien haciendo otra cosa.
    /// </summary>
    [Fact]
    public async Task A_missing_template_is_rejected_and_not_counted()
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync("/api/v1/docs/from-template",
            new { TemplateKey = "no-existe" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var uses = await UsesAsync(client);
        uses.Should().NotContainKey("no-existe", "una plantilla inexistente no puede sumar usos");
    }
}
