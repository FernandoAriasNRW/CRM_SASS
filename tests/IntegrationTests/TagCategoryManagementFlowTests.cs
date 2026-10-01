using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Renombrar y borrar categorías propias.
///
/// Renombrar mueve sus etiquetas (la referencian por nombre). Borrar sólo se puede con la
/// categoría vacía: con etiquetas dentro es un 409, para no borrarlas en cascada y quitarlas de
/// todo lo que las lleva por un clic que parece de orden. Las dos cosas piden poder gestionar
/// todas las etiquetas —administrador o permiso «Full»—, porque afectan a etiquetas de otros.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TagCategoryManagementFlowTests(CrmApiFactory factory)
{
    private const string Categories = "/api/v1/tags/categories";
    private const string MemberPassword = "Miembro-De-Pruebas-2026!";

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private Task<HttpClient> AdminAsync() => LoginAsync("admin@acme.com", "admin123");

    private async Task<HttpClient> NewMemberAsync(HttpClient admin)
    {
        var email = $"miembro.categorias.{Guid.NewGuid():N}@acme.com";
        (await admin.PostAsJsonAsync("/api/v1/users",
            new { Name = "Miembro de categorías", Email = email, Password = MemberPassword, Role = "Member" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        return await LoginAsync(email, MemberPassword);
    }

    private static async Task<JsonElement> NewCategoryAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(Categories, new { name = $"Cat {Guid.NewGuid():N}"[..30] });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Guid> NewTagInAsync(HttpClient client, string category)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tags", new { name = $"En categoría {Guid.NewGuid():N}", category });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> CategoryAsync(HttpClient client, Guid id)
        => (await client.GetFromJsonAsync<JsonElement>(Categories))
            .EnumerateArray().Single(c => c.TryGetProperty("id", out var cid) && cid.ValueKind == JsonValueKind.String && cid.GetGuid() == id);

    private static Guid IdOf(JsonElement element) => element.GetProperty("id").GetGuid();

    [Fact]
    public async Task Renaming_a_category_moves_its_tags()
    {
        var admin = await AdminAsync();
        var category = await NewCategoryAsync(admin);
        var tag = await NewTagInAsync(admin, category.GetProperty("name").GetString()!);
        var newName = $"Renombrada {Guid.NewGuid():N}"[..30];

        var response = await admin.PutAsJsonAsync($"{Categories}/{IdOf(category)}", new { name = newName });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var tags = await admin.GetFromJsonAsync<JsonElement>("/api/v1/tags");
        tags.EnumerateArray().Single(t => IdOf(t) == tag).GetProperty("category").GetString().Should().Be(newName);

        var listed = await CategoryAsync(admin, IdOf(category));
        listed.GetProperty("name").GetString().Should().Be(newName);
        listed.GetProperty("tagCount").GetInt32().Should().Be(1);
        listed.GetProperty("canManage").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Renaming_to_an_existing_or_built_in_name_is_rejected()
    {
        var admin = await AdminAsync();
        var first = await NewCategoryAsync(admin);
        var second = await NewCategoryAsync(admin);

        (await admin.PutAsJsonAsync($"{Categories}/{IdOf(second)}", new { name = first.GetProperty("name").GetString() }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PutAsJsonAsync($"{Categories}/{IdOf(second)}", new { name = "Hito" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync($"{Categories}/{Guid.NewGuid()}", new { name = "Lo que sea" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_category_with_tags_cannot_be_deleted_but_an_empty_one_can()
    {
        var admin = await AdminAsync();
        var category = await NewCategoryAsync(admin);
        var tag = await NewTagInAsync(admin, category.GetProperty("name").GetString()!);

        var blocked = await admin.DeleteAsync($"{Categories}/{IdOf(category)}");
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await blocked.Content.ReadAsStringAsync()).Should().Contain("1 etiqueta");

        (await admin.DeleteAsync($"/api/v1/tags/{tag}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"{Categories}/{IdOf(category)}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await admin.GetFromJsonAsync<JsonElement>(Categories)).EnumerateArray()
            .Should().NotContain(c => c.GetProperty("name").GetString() == category.GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_member_creates_categories_but_cannot_rename_or_delete_them()
    {
        var admin = await AdminAsync();
        var member = await NewMemberAsync(admin);
        var category = await NewCategoryAsync(member);

        (await CategoryAsync(member, IdOf(category))).GetProperty("canManage").GetBoolean().Should().BeFalse();
        (await member.PutAsJsonAsync($"{Categories}/{IdOf(category)}", new { name = "Del miembro" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.DeleteAsync($"{Categories}/{IdOf(category)}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Built_in_categories_report_their_tag_count_and_are_not_manageable()
    {
        var categories = await (await AdminAsync()).GetFromJsonAsync<JsonElement>(Categories);

        var support = categories.EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Support");
        support.GetProperty("tagCount").GetInt32().Should().BeGreaterThanOrEqualTo(10);
        support.GetProperty("canManage").GetBoolean().Should().BeFalse("las predefinidas no se renombran ni se borran");
    }
}
