using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Tags.Application.Abstractions;
using Tags.Domain.Entities;
using Tags.Domain.ValueObjects;
using Tags.Infrastructure.Persistence;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Editar y borrar etiquetas, y quién puede hacerlo:
/// <list type="bullet">
/// <item>quien la creó, sobre las suyas;</item>
/// <item>un administrador, sobre cualquiera;</item>
/// <item>alguien a quien un administrador le dio «Full» sobre etiquetas, sobre cualquiera.</item>
/// </list>
/// Las de equipos y proyectos no se tocan a mano: siguen a su equipo o proyecto.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TagManagementFlowTests(CrmApiFactory factory)
{
    private const string Tags = "/api/v1/tags";
    private const string MemberPassword = "Miembro-De-Pruebas-2026!";

    private sealed record Session(HttpClient Client, Guid UserId, Guid TenantId);

    private async Task<Session> LoginAsync(string email, string password)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me");
        return new Session(client, me.GetProperty("id").GetGuid(), me.GetProperty("tenantId").GetGuid());
    }

    private Task<Session> AdminAsync() => LoginAsync("admin@acme.com", "admin123");

    /// <summary>Un miembro nuevo en cada prueba, para que los permisos de una no se cuelen en otra.</summary>
    private async Task<Session> NewMemberAsync(Session admin)
    {
        var email = $"miembro.etiquetas.{Guid.NewGuid():N}@acme.com";
        var created = await admin.Client.PostAsJsonAsync("/api/v1/users",
            new { Name = "Miembro de etiquetas", Email = email, Password = MemberPassword, Role = "Member" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        return await LoginAsync(email, MemberPassword);
    }

    private static async Task<JsonElement> CreateTagAsync(Session session, string? name = null, string category = "Business")
    {
        var response = await session.Client.PostAsJsonAsync(Tags, new { name = name ?? $"Etiqueta {Guid.NewGuid():N}", category });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Guid IdOf(JsonElement tag) => tag.GetProperty("id").GetGuid();

    private static async Task<JsonElement?> FindAsync(Session session, Guid id)
    {
        var list = await session.Client.GetFromJsonAsync<JsonElement>(Tags);
        return list.EnumerateArray().Where(t => t.GetProperty("id").GetGuid() == id).Cast<JsonElement?>().FirstOrDefault();
    }

    private static Task<HttpResponseMessage> RenameAsync(Session session, Guid id, string name, string category = "Business", string? colorHex = null)
        => session.Client.PutAsJsonAsync($"{Tags}/{id}", new { name, colorHex, category });

    /// <summary>Lo que hace la pantalla de permisos de una persona al guardar la fila «Etiquetas».</summary>
    private static Task<HttpResponseMessage> GrantTagLevelAsync(Session admin, Guid userId, string level)
        => admin.Client.PostAsJsonAsync("/api/v1/permissions", new
        {
            targetType = "User",
            userId,
            permissions = new[] { new { entityType = "Tag", entityId = Guid.Empty, permissionLevel = level } },
        });

    /// <summary>Una etiqueta escrita directamente en la base, para lo que la API no deja crear.</summary>
    private async Task<Guid> InsertTagAsync(Guid tenantId, string category)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TagsDbContext>();
        using var _ = db.AsTenant(tenantId);

        var tag = Tag.Create(tenantId, $"Directa {Guid.NewGuid():N}", "#000000", category, externalReferenceId: Guid.NewGuid());
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag.Id;
    }

    // ── Quien la creó ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_creator_can_edit_and_delete_their_own_tag()
    {
        var member = await NewMemberAsync(await AdminAsync());
        var tag = await CreateTagAsync(member);
        tag.GetProperty("createdBy").GetGuid().Should().Be(member.UserId);

        var newName = $"Renombrada {Guid.NewGuid():N}";
        var edit = await RenameAsync(member, IdOf(tag), newName, "WorkType", "#10b981");

        edit.StatusCode.Should().Be(HttpStatusCode.OK, await edit.Content.ReadAsStringAsync());
        var stored = (await FindAsync(member, IdOf(tag)))!.Value;
        stored.GetProperty("name").GetString().Should().Be(newName);
        stored.GetProperty("category").GetString().Should().Be("WorkType");
        stored.GetProperty("colorHex").GetString().Should().Be("#10B981");

        var delete = await member.Client.DeleteAsync($"{Tags}/{IdOf(tag)}");

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync(member, IdOf(tag))).Should().BeNull();
    }

    [Fact]
    public async Task Editing_without_a_color_keeps_the_one_it_had()
    {
        var member = await NewMemberAsync(await AdminAsync());
        var tag = await CreateTagAsync(member);

        (await RenameAsync(member, IdOf(tag), $"Otro nombre {Guid.NewGuid():N}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await FindAsync(member, IdOf(tag)))!.Value.GetProperty("colorHex").GetString()
            .Should().Be(tag.GetProperty("colorHex").GetString());
    }

    // ── Otro miembro ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_member_cannot_edit_or_delete_it()
    {
        var admin = await AdminAsync();
        var owner = await NewMemberAsync(admin);
        var other = await NewMemberAsync(admin);
        var tag = await CreateTagAsync(owner);

        (await RenameAsync(other, IdOf(tag), "Intento ajeno")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await other.Client.DeleteAsync($"{Tags}/{IdOf(tag)}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await FindAsync(owner, IdOf(tag)))!.Value.GetProperty("name").GetString()
            .Should().Be(tag.GetProperty("name").GetString(), "un 403 no puede haber cambiado nada");
    }

    [Fact]
    public async Task The_list_tells_each_person_which_tags_they_can_manage()
    {
        var admin = await AdminAsync();
        var owner = await NewMemberAsync(admin);
        var other = await NewMemberAsync(admin);
        var tag = await CreateTagAsync(owner);

        (await FindAsync(owner, IdOf(tag)))!.Value.GetProperty("canManage").GetBoolean().Should().BeTrue();
        (await FindAsync(other, IdOf(tag)))!.Value.GetProperty("canManage").GetBoolean().Should().BeFalse();
        (await FindAsync(admin, IdOf(tag)))!.Value.GetProperty("canManage").GetBoolean().Should().BeTrue();
    }

    // ── Administrador ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_admin_can_edit_and_delete_anyones_tag()
    {
        var admin = await AdminAsync();
        var member = await NewMemberAsync(admin);
        var tag = await CreateTagAsync(member);

        (await RenameAsync(admin, IdOf(tag), $"Por el admin {Guid.NewGuid():N}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.Client.DeleteAsync($"{Tags}/{IdOf(tag)}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_deleted_built_in_tag_does_not_come_back_when_provisioning_runs_again()
    {
        var admin = await AdminAsync();
        var list = await admin.Client.GetFromJsonAsync<JsonElement>(Tags);
        var maintenance = list.EnumerateArray().Single(t => t.GetProperty("builtInKey").GetString() == "maintenance");
        maintenance.GetProperty("canManage").GetBoolean().Should().BeTrue("un administrador gestiona también las predefinidas");

        (await admin.Client.DeleteAsync($"{Tags}/{IdOf(maintenance)}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Lo mismo que pasa en cada arranque.
        using (var scope = factory.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IBuiltInTagProvisioner>().ProvisionAsync(admin.TenantId))
                .Should().Be(0);

        var after = await admin.Client.GetFromJsonAsync<JsonElement>(Tags);
        after.EnumerateArray().Should().NotContain(t => t.GetProperty("builtInKey").GetString() == "maintenance");
    }

    [Fact]
    public async Task A_renamed_built_in_tag_keeps_its_new_name_in_both_languages()
    {
        var admin = await AdminAsync();
        var list = await admin.Client.GetFromJsonAsync<JsonElement>(Tags);
        var trial = list.EnumerateArray().Single(t => t.GetProperty("builtInKey").GetString() == "trial-client");

        (await RenameAsync(admin, IdOf(trial), "Cliente en piloto")).StatusCode.Should().Be(HttpStatusCode.OK);

        var english = await admin.Client.GetFromJsonAsync<JsonElement>($"{Tags}?language=en");
        var renamed = english.EnumerateArray().Single(t => IdOf(t) == IdOf(trial));
        renamed.GetProperty("name").GetString().Should().Be("Cliente en piloto", "ya no es la del catálogo; se enseña como la llamaron");

        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IBuiltInTagProvisioner>().ProvisionAsync(admin.TenantId))
            .Should().Be(0, "renombrarla no hace que vuelva la original");
    }

    [Fact]
    public async Task Editing_a_built_in_tag_with_its_english_name_keeps_it_built_in()
    {
        var admin = await AdminAsync();
        var english = await admin.Client.GetFromJsonAsync<JsonElement>($"{Tags}?language=en");
        var partner = english.EnumerateArray().Single(t => t.GetProperty("builtInKey").GetString() == "partner");
        partner.GetProperty("name").GetString().Should().Be("Partner");

        // Lo que manda la pantalla en inglés al cambiar sólo el color: el nombre tal como lo ve.
        (await RenameAsync(admin, IdOf(partner), "Partner", "Business", "#123456")).StatusCode.Should().Be(HttpStatusCode.OK);

        var spanish = (await FindAsync(admin, IdOf(partner)))!.Value;
        spanish.GetProperty("builtInKey").GetString().Should().Be("partner", "cambiar el color no la convierte en propia");
        spanish.GetProperty("name").GetString().Should().Be("Socio");
        spanish.GetProperty("colorHex").GetString().Should().Be("#123456");
    }

    // ── Permiso concedido ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_member_given_full_permission_on_tags_can_manage_anyones()
    {
        var admin = await AdminAsync();
        var owner = await NewMemberAsync(admin);
        var manager = await NewMemberAsync(admin);
        var tag = await CreateTagAsync(owner);

        (await RenameAsync(manager, IdOf(tag), "Antes del permiso")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await GrantTagLevelAsync(admin, manager.UserId, "Full")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await FindAsync(manager, IdOf(tag)))!.Value.GetProperty("canManage").GetBoolean().Should().BeTrue();
        (await RenameAsync(manager, IdOf(tag), $"Con permiso {Guid.NewGuid():N}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await manager.Client.DeleteAsync($"{Tags}/{IdOf(tag)}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Edit_permission_on_tags_is_not_enough_to_manage_other_peoples()
    {
        var admin = await AdminAsync();
        var owner = await NewMemberAsync(admin);
        var member = await NewMemberAsync(admin);
        var tag = await CreateTagAsync(owner);

        (await GrantTagLevelAsync(admin, member.UserId, "Edit")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await RenameAsync(member, IdOf(tag), "Sólo con Edit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_permission_can_only_be_given_to_someone_in_the_organization()
    {
        var response = await GrantTagLevelAsync(await AdminAsync(), Guid.NewGuid(), "Full");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Only_admins_can_give_the_permission()
    {
        var admin = await AdminAsync();
        var member = await NewMemberAsync(admin);

        var response = await GrantTagLevelAsync(member, member.UserId, "Full");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Lo que no se puede ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Project")]
    [InlineData("Team")]
    public async Task Team_and_project_tags_are_not_edited_or_deleted_by_hand(string category)
    {
        var admin = await AdminAsync();
        var id = await InsertTagAsync(admin.TenantId, category);

        (await FindAsync(admin, id))!.Value.GetProperty("canManage").GetBoolean().Should().BeFalse();
        (await RenameAsync(admin, id, "A mano")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.Client.DeleteAsync($"{Tags}/{id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_tag_cannot_be_moved_into_a_team_or_project_category()
    {
        var admin = await AdminAsync();
        var tag = await CreateTagAsync(admin);

        (await RenameAsync(admin, IdOf(tag), "Ahora de proyecto", "Project")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_tag_of_another_organization_is_not_found()
    {
        var admin = await AdminAsync();
        var foreign = await InsertTagAsync(Guid.NewGuid(), TagCategory.Business);

        (await RenameAsync(admin, foreign, "Ajena")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.Client.DeleteAsync($"{Tags}/{foreign}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Renaming_to_a_name_already_in_the_category_returns_409()
    {
        var admin = await AdminAsync();
        var first = await CreateTagAsync(admin);
        var second = await CreateTagAsync(admin);

        var response = await RenameAsync(admin, IdOf(second), first.GetProperty("name").GetString()!);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Editing_validates_like_creating()
    {
        var admin = await AdminAsync();
        var tag = await CreateTagAsync(admin);

        (await RenameAsync(admin, IdOf(tag), "")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RenameAsync(admin, IdOf(tag), "Color raro", colorHex: "red")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RenameAsync(admin, IdOf(tag), "Sin categoría", "No existe")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
