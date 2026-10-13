using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Quién puede compartir.
///
/// Compartir no comprobaba nada: cualquiera con sesión se daba a sí mismo <c>Full</c> sobre lo
/// que quisiera, y quien tenía un elemento restringido borraba su propia restricción dejando de
/// compartirlo consigo. Estas pruebas lo intentan con una persona sin privilegios.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SharingPermissionFlowTests(CrmApiFactory factory)
{
    private const string MemberPassword = "Miembro2026!x";

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
        var created = await admin.PostAsJsonAsync("/api/v1/users", new { Name = name, Email = email, Password = MemberPassword, Role = "Member" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (await LoginAsync(email, MemberPassword), id);
    }

    private static async Task<Guid> ATaskAsync(HttpClient client)
    {
        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/tasks?pageSize=1");
        return list.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private async Task<string?> LevelAsync(Guid userId, Guid taskId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await db.EntityPermissions.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.UserId == userId && p.EntityType == "Task" && p.EntityId == taskId)
            .Select(p => p.PermissionLevel)
            .FirstOrDefaultAsync();
    }

    [Fact]
    public async Task Nobody_can_give_themselves_full_access()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var (member, memberId) = await MemberAsync(admin, "Ambiciosa");
        var taskId = await ATaskAsync(admin);

        (await member.PutAsJsonAsync($"/api/v1/sharing/Task/{taskId}/{memberId}", new { Level = "Full" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await LevelAsync(memberId, taskId)).Should().BeNull("no se ha guardado nada");
    }

    [Fact]
    public async Task A_restriction_cannot_be_removed_by_who_it_restricts()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var (member, memberId) = await MemberAsync(admin, "Restringida");
        var taskId = await ATaskAsync(admin);
        var tenantId = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me")).GetProperty("tenantId").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.EntityPermissions.Add(EntityPermission.CreateForUser(tenantId, memberId, "Task", taskId, "None"));
            await db.SaveChangesAsync();
        }

        (await member.DeleteAsync($"/api/v1/sharing/Task/{taskId}/{memberId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await LevelAsync(memberId, taskId)).Should().Be("None", "la restricción sigue");
    }

    [Fact]
    public async Task Whoever_can_edit_shares_up_to_edit_with_someone_of_the_organization()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var (member, _) = await MemberAsync(admin, "Editora");
        var (_, colleagueId) = await MemberAsync(admin, "Colega");
        var taskId = await ATaskAsync(admin);

        (await member.PutAsJsonAsync($"/api/v1/sharing/Task/{taskId}/{colleagueId}", new { Level = "Edit" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LevelAsync(colleagueId, taskId)).Should().Be("Edit");

        (await member.PutAsJsonAsync($"/api/v1/sharing/Task/{taskId}/{colleagueId}", new { Level = "Full" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "no se da más de lo que se tiene");

        (await member.PutAsJsonAsync($"/api/v1/sharing/Task/{taskId}/{Guid.NewGuid()}", new { Level = "View" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "con alguien que no está en la organización no");

        (await member.DeleteAsync($"/api/v1/sharing/Task/{taskId}/{colleagueId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "dejar de compartir exige Full");
        (await admin.DeleteAsync($"/api/v1/sharing/Task/{taskId}/{colleagueId}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
