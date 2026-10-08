using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Al crear un proyecto o un equipo nace su etiqueta, para poder etiquetar trabajo con él.
///
/// <c>AutomaticTags</c> lo hacía escuchando los eventos de alta, pero ni Projects ni Teams
/// repartían sus eventos en proceso: el manejador estaba escrito y no se ejecutaba nunca, y ninguna
/// prueba lo miraba. Esta crea el proyecto o el equipo y busca la etiqueta.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AutomaticTagsFlowTests(CrmApiFactory factory)
{
    private async Task<HttpClient> AdminAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<List<string?>> TagNamesAsync(HttpClient client)
    {
        var tags = await client.GetFromJsonAsync<JsonElement>("/api/v1/tags");
        var list = tags.ValueKind == JsonValueKind.Array ? tags : tags.GetProperty("items");
        return list.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
    }

    [Fact]
    public async Task A_new_project_gets_its_tag()
    {
        var admin = await AdminAsync();
        var name = "Proyecto etiquetado " + Guid.NewGuid().ToString("N")[..8];

        await TestProjects.CreateAsync(admin, name);

        (await TagNamesAsync(admin)).Should().Contain(name);
    }

    [Fact]
    public async Task A_new_team_gets_its_tag()
    {
        var admin = await AdminAsync();
        var name = "Equipo etiquetado " + Guid.NewGuid().ToString("N")[..8];

        (await admin.PostAsJsonAsync("/api/v1/teams", new { name, description = "", memberIds = Array.Empty<Guid>() })).EnsureSuccessStatusCode();

        (await TagNamesAsync(admin)).Should().Contain(name);
    }
}
