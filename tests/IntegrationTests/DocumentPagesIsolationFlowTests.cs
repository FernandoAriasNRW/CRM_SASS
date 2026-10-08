using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Las páginas de un documento son de su organización y respetan el permiso sobre su documento.
///
/// <c>Page</c> no llevaba <c>TenantId</c> y se buscaba solo por su identificador, así que el filtro
/// global de inquilino no la alcanzaba: sabiendo el identificador de una página de otra
/// organización, se podía editar, mover, borrar o anotar. Y los comandos de página comprobaban el
/// permiso sobre los documentos en general, no sobre el documento de la página: quien solo podía
/// leer un documento podía reescribir sus páginas.
///
/// El «intruso» es un administrador de verdad de otra organización: con un token inventado la
/// autorización lo para antes, porque su usuario no existe, y la prueba no vería el fallo.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DocumentPagesIsolationFlowTests(CrmApiFactory factory)
{
    private const string Password = "Intruso2026!x";

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    /// <summary>Un administrador de otra organización, con su cuenta guardada de verdad.</summary>
    private async Task<HttpClient> StrangerAsync()
    {
        var email = $"intruso.{Guid.NewGuid():N}@otra.test";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = User.Create(
                DateTime.UtcNow, Guid.NewGuid(), "Intruso", Email.Create(email).Value!,
                PasswordHash.Create(DateTime.UtcNow, Password), UserRole.Admin).Value!;
            db.User.Add(user);
            await db.SaveChangesAsync();
        }
        return await LoginAsync(email, Password);
    }

    private static async Task<(Guid DocumentId, Guid PageId)> DocumentWithPageAsync(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/api/v1/docs/", new
        {
            Title = "Documento privado " + Guid.NewGuid().ToString("N")[..6],
            Description = "Solo de mi organización",
            Type = 1,
        });
        created.EnsureSuccessStatusCode();
        var documentId = Guid.Parse((await created.Content.ReadAsStringAsync()).Trim('"'));

        var page = await client.PostAsJsonAsync($"/api/v1/docs/{documentId}/pages", new { ParentPageId = (Guid?)null, Title = "Página privada" });
        page.EnsureSuccessStatusCode();
        var pageId = Guid.Parse((await page.Content.ReadAsStringAsync()).Trim('"'));

        (await client.PutAsJsonAsync($"/api/v1/docs/pages/{pageId}", new { Title = "Página privada", Content = "Contenido original" }))
            .EnsureSuccessStatusCode();
        return (documentId, pageId);
    }

    private static async Task<JsonElement?> PageAsync(HttpClient client, Guid documentId, Guid pageId)
    {
        var pages = await client.GetFromJsonAsync<JsonElement>($"/api/v1/docs/{documentId}/pages");
        return pages.EnumerateArray().Where(p => p.GetProperty("id").GetGuid() == pageId).Select(p => (JsonElement?)p).FirstOrDefault();
    }

    [Fact]
    public async Task Another_organization_cannot_touch_my_pages_even_knowing_their_id()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var (documentId, pageId) = await DocumentWithPageAsync(admin);
        var (_, otherPageId) = await DocumentWithPageAsync(admin);
        var stranger = await StrangerAsync();

        (await stranger.PutAsJsonAsync($"/api/v1/docs/pages/{pageId}", new { Title = "Cambiado", Content = "Lo he reescrito yo" }))
            .IsSuccessStatusCode.Should().BeFalse("la página es de otra organización");
        (await stranger.PutAsJsonAsync($"/api/v1/docs/pages/{pageId}/move", new { ParentPageId = otherPageId, Order = 0 }))
            .IsSuccessStatusCode.Should().BeFalse();
        (await stranger.PostAsJsonAsync($"/api/v1/docs/pages/{pageId}/annotations", new { QuotedText = "Contenido" }))
            .IsSuccessStatusCode.Should().BeFalse();
        (await stranger.DeleteAsync($"/api/v1/docs/pages/{pageId}"))
            .IsSuccessStatusCode.Should().BeFalse();

        // Lo que importa no es el código de estado sino que la página siga como estaba.
        var page = await PageAsync(admin, documentId, pageId);
        page.Should().NotBeNull("no la ha borrado");
        page!.Value.GetProperty("content").GetString().Should().Be("Contenido original");
        page.Value.GetProperty("parentPageId").ValueKind.Should().Be(JsonValueKind.Null, "no la ha movido");
        (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/docs/pages/{pageId}/annotations"))
            .EnumerateArray().Should().BeEmpty("no la ha anotado");
    }

    /// <summary>Quien solo puede leer un documento no reescribe, mueve ni borra sus páginas.</summary>
    [Fact]
    public async Task Read_only_on_a_document_means_its_pages_are_read_only()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var (documentId, pageId) = await DocumentWithPageAsync(admin);

        var email = $"lectora.{Guid.NewGuid():N}@acme.com";
        var created = await admin.PostAsJsonAsync("/api/v1/users", new { Name = "Lectora", Email = email, Password, Role = "Member" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var readerId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var tenantId = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me")).GetProperty("tenantId").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.EntityPermissions.Add(EntityPermission.CreateForUser(tenantId, readerId, "Document", documentId, "Read"));
            await db.SaveChangesAsync();
        }

        var reader = await LoginAsync(email, Password);
        (await PageAsync(reader, documentId, pageId)).Should().NotBeNull("puede leerla");

        (await reader.PutAsJsonAsync($"/api/v1/docs/pages/{pageId}", new { Title = "Cambiado", Content = "Lo he reescrito yo" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reader.PutAsJsonAsync($"/api/v1/docs/pages/{pageId}/move", new { ParentPageId = (Guid?)null, Order = 3 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reader.DeleteAsync($"/api/v1/docs/pages/{pageId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await PageAsync(admin, documentId, pageId))!.Value.GetProperty("content").GetString().Should().Be("Contenido original");
    }

    /// <summary>Una página nueva no puede colgar de una página de otro documento.</summary>
    [Fact]
    public async Task A_new_page_cannot_hang_from_another_documents_page()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var (documentId, _) = await DocumentWithPageAsync(admin);
        var (_, foreignPageId) = await DocumentWithPageAsync(admin);

        (await admin.PostAsJsonAsync($"/api/v1/docs/{documentId}/pages", new { ParentPageId = foreignPageId, Title = "Huérfana" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
