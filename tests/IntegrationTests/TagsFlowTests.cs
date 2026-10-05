using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Etiquetas y sus categorías.
///
/// Existen porque los dos endpoints eran marcadores: el GET devolvía siempre un array vacío con la
/// consulta comentada y el POST respondía 200 sin guardar nada. Ninguna prueba lo miraba.
///
/// Las predefinidas (hitos, negocio, seguridad, tipo de trabajo, fase de desarrollo) se crean al
/// arrancar para cada organización y se muestran en el idioma que pida la pantalla. Los listados no
/// se comparan con un número exacto: otras pruebas crean proyectos, y cada proyecto crea su etiqueta
/// (<c>AutomaticTags</c>).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TagsFlowTests(CrmApiFactory factory)
{
    private const string Tags = "/api/v1/tags";
    private const string Categories = "/api/v1/tags/categories";

    private static readonly string[] BuiltInCategories =
        ["Team", "Project", "Milestone", "Business", "Security", "WorkType", "DevelopmentPhase"];

    private async Task<HttpClient> AdminAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<List<JsonElement>> GetArrayAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array, "los listados son arrays planos, sin paginar");
        return body.EnumerateArray().ToList();
    }

    private static JsonElement ByKey(IEnumerable<JsonElement> tags, string key)
        => tags.Single(t => t.GetProperty("builtInKey").GetString() == key);

    private static string? Str(JsonElement element, string property) => element.GetProperty(property).GetString();

    // ── Listado ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_returns_the_built_in_tags_in_spanish_by_default()
    {
        var tags = await GetArrayAsync(await AdminAsync(), Tags);

        var vip = ByKey(tags, "vip-client");
        Str(vip, "name").Should().Be("Cliente VIP");
        Str(vip, "category").Should().Be("Business");
        Str(vip, "categoryLabel").Should().Be("Negocio");
        vip.GetProperty("id").GetGuid().Should().NotBeEmpty();

        Str(ByKey(tags, "requirements"), "name").Should().Be("Requisitos");
        Str(ByKey(tags, "requirements"), "categoryLabel").Should().Be("Fase de desarrollo");
        Str(ByKey(tags, "feature"), "categoryLabel").Should().Be("Tipo de trabajo");

        // El hito propio de la demostración no es predefinido: sale tal cual, sin clave.
        var demo = tags.Single(t => Str(t, "name") == "🚀 Q3 Release");
        demo.GetProperty("builtInKey").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task List_in_english_translates_built_in_tags_and_categories_but_not_the_organizations_own()
    {
        var tags = await GetArrayAsync(await AdminAsync(), $"{Tags}?language=en");

        Str(ByKey(tags, "vip-client"), "name").Should().Be("VIP client");
        Str(ByKey(tags, "vip-client"), "categoryLabel").Should().Be("Business");
        Str(ByKey(tags, "requirements"), "name").Should().Be("Requirements");
        Str(ByKey(tags, "requirements"), "categoryLabel").Should().Be("Development phase");

        tags.Should().Contain(t => Str(t, "name") == "🚀 Q3 Release");
    }

    [Fact]
    public async Task Built_in_tags_are_provisioned_once_even_though_startup_and_seeding_both_run()
    {
        var tags = await GetArrayAsync(await AdminAsync(), Tags);

        var keys = tags.Select(t => Str(t, "builtInKey")).Where(k => k is not null).ToList();
        keys.Should().OnlyHaveUniqueItems();
        keys.Should().Contain(["poc", "mvp", "beta", "release", "api-client", "web-client", "vulnerability", "bug", "deployment"]);
    }

    [Fact]
    public async Task There_are_no_priority_or_tech_tags()
    {
        var tags = await GetArrayAsync(await AdminAsync(), Tags);

        tags.Select(t => Str(t, "category")).Should().NotContain(["Priority", "Tech"],
            "la prioridad ya es un campo de tareas y tickets, y «Tech» se quitó");
    }

    [Fact]
    public async Task List_requires_a_session()
    {
        var response = await factory.CreateClient().GetAsync(Tags);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Categorías ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Categories_start_with_the_built_in_ones_labelled_in_spanish()
    {
        var categories = await GetArrayAsync(await AdminAsync(), Categories);

        categories.Take(BuiltInCategories.Length).Select(c => Str(c, "name")).Should().Equal(BuiltInCategories);
        categories.Select(c => Str(c, "name")).Should().NotContain("Priority");

        var workType = categories.Single(c => Str(c, "name") == "WorkType");
        Str(workType, "label").Should().Be("Tipo de trabajo");
        workType.GetProperty("isCustom").GetBoolean().Should().BeFalse();
        workType.GetProperty("isAutomatic").GetBoolean().Should().BeFalse();

        categories.Single(c => Str(c, "name") == "Project").GetProperty("isAutomatic").GetBoolean().Should().BeTrue();
        categories.Single(c => Str(c, "name") == "Team").GetProperty("isAutomatic").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Categories_in_english()
    {
        var categories = await GetArrayAsync(await AdminAsync(), $"{Categories}?language=en");

        Str(categories.Single(c => Str(c, "name") == "Milestone"), "label").Should().Be("Milestone");
        Str(categories.Single(c => Str(c, "name") == "DevelopmentPhase"), "label").Should().Be("Development phase");
    }

    [Fact]
    public async Task A_custom_category_is_created_listed_and_accepts_tags()
    {
        var client = await AdminAsync();
        var name = $"Clientes {Guid.NewGuid():N}"[..30];

        var response = await client.PostAsJsonAsync(Categories, new { name });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Str(created, "name").Should().Be(name);
        created.GetProperty("isCustom").GetBoolean().Should().BeTrue();
        created.GetProperty("id").GetGuid().Should().NotBeEmpty();

        var listed = (await GetArrayAsync(client, Categories)).Single(c => Str(c, "name") == name);
        Str(listed, "label").Should().Be(name, "una categoría propia se muestra como se llama, en cualquier idioma");

        var tag = await client.PostAsJsonAsync(Tags, new { name = "Beta", category = name });
        tag.StatusCode.Should().Be(HttpStatusCode.Created,
            "el nombre se repite con el hito predefinido «Beta», pero en otra categoría: " + await tag.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_repeated_custom_category_returns_409()
    {
        var client = await AdminAsync();
        var name = $"Repetida {Guid.NewGuid():N}"[..30];
        (await client.PostAsJsonAsync(Categories, new { name })).StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await client.PostAsJsonAsync(Categories, new { name = name.ToUpperInvariant() });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "mayúsculas aparte, es la misma categoría");
    }

    [Theory]
    [InlineData("Milestone")]
    [InlineData("hito")]
    [InlineData("Work type")]
    [InlineData("")]
    public async Task A_custom_category_cannot_be_empty_or_repeat_a_built_in_one(string name)
    {
        var response = await (await AdminAsync()).PostAsJsonAsync(Categories, new { name });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    // ── Alta de etiquetas ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_saves_the_tag_and_returns_201()
    {
        var client = await AdminAsync();
        var name = $"Cliente prioritario {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync(Tags, new { name, colorHex = "#3b82f6", category = "Business" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("id").GetGuid().Should().NotBeEmpty();
        Str(created, "name").Should().Be(name);
        Str(created, "colorHex").Should().Be("#3B82F6", "el color se guarda en mayúsculas, como los predefinidos");
        Str(created, "category").Should().Be("Business");
        created.GetProperty("builtInKey").ValueKind.Should().Be(JsonValueKind.Null);

        // Lo que importa es que se guardó: el endpoint anterior respondía bien y no guardaba nada.
        var listed = (await GetArrayAsync(client, Tags)).Single(t => t.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid());
        Str(listed, "name").Should().Be(name);
    }

    [Fact]
    public async Task Create_without_color_uses_a_neutral_grey()
    {
        var response = await (await AdminAsync()).PostAsJsonAsync(Tags, new { name = $"Sin color {Guid.NewGuid():N}", category = "WorkType" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        Str(await response.Content.ReadFromJsonAsync<JsonElement>(), "colorHex").Should().Be("#6B7280");
    }

    [Fact]
    public async Task Create_ignores_a_tenant_sent_in_the_body()
    {
        var client = await AdminAsync();
        var name = $"Inquilino ajeno {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync(Tags, new { tenantId = Guid.NewGuid(), name, category = "Security" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        // Si se hubiera guardado con el inquilino del cuerpo, el filtro global la escondería.
        (await GetArrayAsync(client, Tags)).Select(t => Str(t, "name")).Should().Contain(name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Priority")]
    [InlineData("No existe")]
    public async Task Create_needs_an_existing_category(string? category)
    {
        var response = await (await AdminAsync()).PostAsJsonAsync(Tags, new { name = $"Sin categoría {Guid.NewGuid():N}", category });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Team")]
    [InlineData("Project")]
    public async Task Team_and_project_tags_cannot_be_created_by_hand(string category)
    {
        var response = await (await AdminAsync()).PostAsJsonAsync(Tags, new { name = $"A mano {Guid.NewGuid():N}", category });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "se crean solas con el equipo o el proyecto");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_without_name_is_rejected_with_400(string name)
    {
        var response = await (await AdminAsync()).PostAsJsonAsync(Tags, new { name, category = "Business" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_a_name_longer_than_the_column_is_rejected_with_400()
    {
        var response = await (await AdminAsync()).PostAsJsonAsync(Tags, new { name = new string('x', 101), category = "Business" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_an_invalid_color_is_rejected_with_400()
    {
        var response = await (await AdminAsync()).PostAsJsonAsync(Tags,
            new { name = $"Color raro {Guid.NewGuid():N}", colorHex = "red", category = "Business" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Cliente VIP")]
    [InlineData("VIP client")]
    public async Task Repeating_a_built_in_tag_in_either_language_returns_409(string name)
    {
        var client = await AdminAsync();

        var response = await client.PostAsJsonAsync(Tags, new { name, category = "Business" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await GetArrayAsync(client, Tags)).Count(t => Str(t, "builtInKey") == "vip-client").Should().Be(1);
    }
}
