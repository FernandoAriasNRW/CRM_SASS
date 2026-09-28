using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// El listado y el alta de etiquetas.
///
/// Existen porque los dos endpoints eran marcadores: el GET devolvía siempre un array vacío con la
/// consulta comentada y el POST respondía 200 sin guardar nada, mientras la tabla tenía las seis
/// etiquetas que siembra <c>TagsSeeder</c>. Ninguna prueba lo miraba, así que nadie lo notó.
///
/// El listado no se compara con exactamente seis: otras pruebas crean proyectos, y cada proyecto
/// crea su etiqueta (<c>EtiquetasAutomaticas</c>). Se comprueba que las sembradas están.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TagsFlowTests(CrmApiFactory factory)
{
    private const string Tags = "/api/v1/tags";

    private static readonly string[] SeededNames =
    [
        "🔥 Crítico", "⚡ Backend C#", "🎨 Frontend Angular", "🔒 Seguridad", "⭐ VIP Client", "🚀 Q3 Release",
    ];

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

    private static async Task<List<JsonElement>> ListAsync(HttpClient client)
    {
        var response = await client.GetAsync(Tags);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array, "el listado es un array plano, sin paginar");
        return body.EnumerateArray().ToList();
    }

    [Fact]
    public async Task List_returns_the_seeded_tags()
    {
        var client = await AdminAsync();

        var tags = await ListAsync(client);

        tags.Select(t => t.GetProperty("name").GetString())
            .Should().Contain(SeededNames);

        var critical = tags.Single(t => t.GetProperty("name").GetString() == "🔥 Crítico");
        critical.GetProperty("colorHex").GetString().Should().Be("#EF4444");
        critical.GetProperty("category").GetString().Should().Be("Priority");
        critical.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task List_requires_a_session()
    {
        var response = await factory.CreateClient().GetAsync(Tags);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_saves_the_tag_and_returns_201()
    {
        var client = await AdminAsync();
        var name = $"Cliente prioritario {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync(Tags, new { name, colorHex = "#3b82f6", category = "Business" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("id").GetGuid().Should().NotBeEmpty();
        created.GetProperty("name").GetString().Should().Be(name);
        created.GetProperty("colorHex").GetString().Should().Be("#3B82F6", "el color se guarda en mayúsculas, como los sembrados");
        created.GetProperty("category").GetString().Should().Be("Business");

        // Lo que importa es que se guardó: el endpoint anterior respondía bien y no guardaba nada.
        var listed = (await ListAsync(client)).Single(t => t.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid());
        listed.GetProperty("name").GetString().Should().Be(name);
    }

    [Fact]
    public async Task Create_without_color_or_category_uses_the_defaults()
    {
        var client = await AdminAsync();

        var response = await client.PostAsJsonAsync(Tags, new { name = $"Sin color {Guid.NewGuid():N}" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("colorHex").GetString().Should().Be("#6B7280");
        created.GetProperty("category").GetString().Should().Be("General");
    }

    [Fact]
    public async Task Create_ignores_a_tenant_sent_in_the_body()
    {
        var client = await AdminAsync();
        var name = $"Inquilino ajeno {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync(Tags, new { tenantId = Guid.NewGuid(), name });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        // Si se hubiera guardado con el inquilino del cuerpo, el filtro global la escondería.
        (await ListAsync(client)).Select(t => t.GetProperty("name").GetString()).Should().Contain(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_without_name_is_rejected_with_400(string name)
    {
        var client = await AdminAsync();

        var response = await client.PostAsJsonAsync(Tags, new { name });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_a_name_longer_than_the_column_is_rejected_with_400()
    {
        var client = await AdminAsync();

        var response = await client.PostAsJsonAsync(Tags, new { name = new string('x', 101) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_an_invalid_color_is_rejected_with_400()
    {
        var client = await AdminAsync();

        var response = await client.PostAsJsonAsync(Tags, new { name = $"Color raro {Guid.NewGuid():N}", colorHex = "red" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_an_existing_name_returns_409_instead_of_failing_on_the_unique_index()
    {
        var client = await AdminAsync();

        var response = await client.PostAsJsonAsync(Tags, new { name = "🔥 Crítico" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await ListAsync(client)).Count(t => t.GetProperty("name").GetString() == "🔥 Crítico").Should().Be(1);
    }
}
