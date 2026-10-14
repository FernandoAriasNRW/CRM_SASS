using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Anotaciones, subidas, espacios y carpetas piden permiso.
///
/// Los hacía cualquiera de la organización, aunque su rol solo le dejara ver: anotar un documento
/// que tenía vedado, borrar la anotación de otra persona con su hilo, subir ficheros, crear y
/// renombrar espacios. Estas pruebas lo intentan con personas con permisos recortados.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EntityAuthorizationFlowTests(CrmApiFactory factory)
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

    /// <summary>Un permiso de esta persona sobre un tipo entero (Guid.Empty) o sobre un elemento.</summary>
    private async Task RestrictAsync(HttpClient admin, Guid userId, string type, Guid entityId, string level)
    {
        var tenantId = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me")).GetProperty("tenantId").GetGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.EntityPermissions.Add(EntityPermission.CreateForUser(tenantId, userId, type, entityId, level));
        await db.SaveChangesAsync();
    }

    private static async Task<(Guid DocumentId, Guid PageId)> DocumentAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/docs/", new { Title = "Con anotaciones " + Guid.NewGuid().ToString("N")[..6], Description = "", Type = 1 });
        created.EnsureSuccessStatusCode();
        var documentId = Guid.Parse((await created.Content.ReadAsStringAsync()).Trim('"'));
        var pages = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/docs/{documentId}/pages");
        return (documentId, pages.EnumerateArray().First().GetProperty("id").GetGuid());
    }

    private static async Task<HttpResponseMessage> AnnotateAsync(HttpClient client, Guid pageId)
        => await client.PostAsJsonAsync($"/api/v1/docs/pages/{pageId}/annotations", new { QuotedText = "un trozo" });

    [Fact]
    public async Task Whoever_cannot_see_a_document_cannot_annotate_it()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var (documentId, pageId) = await DocumentAsync(admin);
        var (member, memberId) = await MemberAsync(admin, "Vetada");
        await RestrictAsync(admin, memberId, "Document", documentId, "None");

        (await AnnotateAsync(member, pageId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_reader_comments_but_only_its_author_or_an_editor_deletes_the_annotation()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var (documentId, pageId) = await DocumentAsync(admin);
        var (author, authorId) = await MemberAsync(admin, "Autora");
        var (other, otherId) = await MemberAsync(admin, "Otra");
        await RestrictAsync(admin, authorId, "Document", documentId, "Read");
        await RestrictAsync(admin, otherId, "Document", documentId, "Read");

        var created = await AnnotateAsync(author, pageId);
        created.StatusCode.Should().Be(HttpStatusCode.OK, "quien puede leer el documento puede comentarlo");
        var annotationId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetGuid();

        (await other.DeleteAsync($"/api/v1/docs/annotations/{annotationId}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "no es suya y solo puede leer el documento");
        (await author.DeleteAsync($"/api/v1/docs/annotations/{annotationId}")).IsSuccessStatusCode
            .Should().BeTrue("la suya la puede borrar");
    }

    [Fact]
    public async Task Whoever_only_reads_documents_cannot_upload_files()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var (reader, readerId) = await MemberAsync(admin, "Lectora");
        await RestrictAsync(admin, readerId, "Document", Guid.Empty, "Read");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("hola"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "notas.txt");

        (await reader.PostAsync("/api/v1/docs/upload", form)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Whoever_only_reads_projects_cannot_create_or_rename_spaces()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var (reader, readerId) = await MemberAsync(admin, "Mirona");
        await RestrictAsync(admin, readerId, "Project", Guid.Empty, "Read");

        var space = await admin.PostAsJsonAsync("/api/v1/spaces", new { name = "Espacio " + Guid.NewGuid().ToString("N")[..6], description = "", color = "#3366ff" });
        space.StatusCode.Should().Be(HttpStatusCode.Created);
        var spaceId = (await space.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await reader.PostAsJsonAsync("/api/v1/spaces", new { name = "Mío", description = "", color = "#000000" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reader.PatchAsJsonAsync($"/api/v1/spaces/{spaceId}", new { name = "Renombrado", description = "", color = "#000000" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reader.PostAsJsonAsync("/api/v1/folders", new { spaceId, name = "Carpeta" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_folder_needs_a_space_of_the_organization()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");

        (await admin.PostAsJsonAsync("/api/v1/folders", new { spaceId = Guid.NewGuid(), name = "Huérfana" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
