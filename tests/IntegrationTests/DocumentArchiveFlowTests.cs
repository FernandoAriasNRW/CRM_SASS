using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Las listas de Documentos: archivados, papelera, míos y compartidos conmigo.
///
/// El documento tenía la columna de archivado y nada la usaba, la papelera no tenía salida, y el
/// menú lateral ofrecía «Mis documentos», «Compartidos conmigo» y «Archivados» sobre una lista que
/// no filtraba: los tres enseñaban todos los documentos.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DocumentArchiveFlowTests(CrmApiFactory factory)
{
    private const string Password = "Miembro2026!x";

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private async Task<(HttpClient Client, Guid Id)> MemberAsync(HttpClient admin, string name)
    {
        var email = $"{name.ToLowerInvariant()}.{Guid.NewGuid():N}@acme.com";
        var created = await admin.PostAsJsonAsync("/api/v1/users", new { Name = name, Email = email, Password, Role = "Member" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (await LoginAsync(email, Password), id);
    }

    private static async Task<Guid> DocumentAsync(HttpClient client, string title)
    {
        var created = await client.PostAsJsonAsync("/api/v1/docs/", new { Title = title, Description = "", Type = 1 });
        created.EnsureSuccessStatusCode();
        return Guid.Parse((await created.Content.ReadAsStringAsync()).Trim('"'));
    }

    private static async Task<List<Guid>> ListAsync(HttpClient client, string? filter = null)
    {
        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/docs/" + (filter is null ? "" : $"?filter={filter}"));
        return list.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).ToList();
    }

    [Fact]
    public async Task Archiving_moves_a_document_out_of_the_list_and_back()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var id = await DocumentAsync(admin, "Para archivar " + Guid.NewGuid().ToString("N")[..6]);

        (await admin.PostAsync($"/api/v1/docs/{id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(admin)).Should().NotContain(id, "archivado sale de la lista");
        (await ListAsync(admin, "archived")).Should().Contain(id);

        (await admin.PostAsync($"/api/v1/docs/{id}/unarchive", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(admin)).Should().Contain(id);
        (await ListAsync(admin, "archived")).Should().NotContain(id);
    }

    [Fact]
    public async Task A_deleted_document_can_be_restored_from_the_trash()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var id = await DocumentAsync(admin, "Para la papelera " + Guid.NewGuid().ToString("N")[..6]);

        (await admin.DeleteAsync($"/api/v1/docs/{id}")).EnsureSuccessStatusCode();
        (await ListAsync(admin)).Should().NotContain(id);
        (await ListAsync(admin, "trash")).Should().Contain(id, "la papelera tiene que enseñar lo borrado");

        (await admin.PostAsync($"/api/v1/docs/{id}/restore", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(admin)).Should().Contain(id);
        (await ListAsync(admin, "trash")).Should().NotContain(id);
    }

    [Fact]
    public async Task Mine_and_shared_with_me_filter_the_list()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var (member, memberId) = await MemberAsync(admin, "Autora");
        var adminDoc = await DocumentAsync(admin, "Del admin " + Guid.NewGuid().ToString("N")[..6]);
        var memberDoc = await DocumentAsync(member, "De la autora " + Guid.NewGuid().ToString("N")[..6]);

        var mine = await ListAsync(member, "mine");
        mine.Should().Contain(memberDoc).And.NotContain(adminDoc);

        (await ListAsync(member, "shared")).Should().NotContain(adminDoc);
        (await admin.PutAsJsonAsync($"/api/v1/sharing/Document/{adminDoc}/{memberId}", new { Level = "View" })).EnsureSuccessStatusCode();
        (await ListAsync(member, "shared")).Should().Contain(adminDoc).And.NotContain(memberDoc);
    }

    [Fact]
    public async Task Whoever_only_reads_a_document_cannot_archive_it()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var id = await DocumentAsync(admin, "Solo lectura " + Guid.NewGuid().ToString("N")[..6]);
        var (reader, readerId) = await MemberAsync(admin, "Lectora");
        var tenantId = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me")).GetProperty("tenantId").GetGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.EntityPermissions.Add(EntityPermission.CreateForUser(tenantId, readerId, "Document", id, "Read"));
            await db.SaveChangesAsync();
        }

        (await reader.PostAsync($"/api/v1/docs/{id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
