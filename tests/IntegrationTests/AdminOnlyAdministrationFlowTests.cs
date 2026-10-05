using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Gestionar personas y permisos exige ser administrador, <b>en la API</b>, no sólo en la pantalla.
///
/// Encontrado probando con un miembro contra la aplicación levantada: la pantalla de
/// administración tenía su guarda, pero los endpoints sólo pedían haber iniciado sesión. Un miembro
/// creaba un usuario con rol «Admin» y la API respondía 201, así que cualquiera de la organización
/// podía hacerse administrador en dos peticiones.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AdminOnlyAdministrationFlowTests(CrmApiFactory factory)
{
    private const string MemberPassword = "Miembro2026!x";

    private async Task<HttpClient> ClientWithTokenAsync(string email, string password)
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private Task<HttpClient> AdminAsync() => ClientWithTokenAsync("admin@acme.com", "admin123");

    /// <summary>Un miembro recién creado por el administrador, con su propia sesión.</summary>
    private async Task<(HttpClient client, Guid id)> MemberAsync()
    {
        var admin = await AdminAsync();
        var email = $"miembro.{Guid.NewGuid():N}@acme.com";

        var created = await admin.PostAsJsonAsync("/api/v1/users",
            new { Name = "Miembro de prueba", Email = email, Password = MemberPassword, Role = "Member" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (await ClientWithTokenAsync(email, MemberPassword), id);
    }

    [Fact]
    public async Task A_member_cannot_create_an_admin()
    {
        var (member, _) = await MemberAsync();

        var response = await member.PostAsJsonAsync("/api/v1/users", new
        {
            Name = "Colado",
            Email = $"colado.{Guid.NewGuid():N}@acme.com",
            Password = MemberPassword,
            Role = "Admin"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>La otra mitad de la misma escalada: cambiarse el rol a sí mismo.</summary>
    [Fact]
    public async Task A_member_cannot_change_their_role()
    {
        var (member, id) = await MemberAsync();

        var response = await member.PutAsJsonAsync($"/api/v1/users/{id}",
            new { Name = "Miembro de prueba", Email = (string?)null, Role = "Admin" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_member_cannot_delete_people()
    {
        var (member, _) = await MemberAsync();
        var (_, other) = await MemberAsync();

        var response = await member.DeleteAsync($"/api/v1/users/{other}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Ni leer la matriz de permisos ni, sobre todo, darse permisos.</summary>
    [Fact]
    public async Task A_member_cannot_see_or_change_permissions()
    {
        var (member, id) = await MemberAsync();

        var read = await member.GetAsync("/api/v1/permissions?targetType=Role");
        read.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var write = await member.PostAsJsonAsync("/api/v1/permissions", new
        {
            TargetType = "User",
            UserId = id,
            Permissions = new[] { new { EntityType = "Settings", EntityId = Guid.Empty, PermissionLevel = "Full" } }
        });
        write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Lo que un miembro sí necesita sigue abierto: el directorio de personas, que usan las
    /// menciones y los selectores de responsable, y su propio perfil.
    /// </summary>
    [Fact]
    public async Task A_member_still_sees_the_directory_and_their_profile()
    {
        var (member, _) = await MemberAsync();

        (await member.GetAsync("/api/v1/users/tenant")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await member.GetAsync("/api/v1/auth/users/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_admin_still_manages_people_and_permissions()
    {
        var admin = await AdminAsync();

        (await admin.GetAsync("/api/v1/permissions?targetType=Role")).StatusCode.Should().Be(HttpStatusCode.OK);

        var (_, id) = await MemberAsync();
        var change = await admin.PutAsJsonAsync($"/api/v1/users/{id}",
            new { Name = "Miembro renombrado", Email = (string?)null, Role = "Member" });
        change.StatusCode.Should().Be(HttpStatusCode.OK, await change.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Borrar a un administrador, habiendo otros, funciona.
    ///
    /// Salió al limpiar los datos de la sonda: la cuenta de administradores que protege al último
    /// no se podía traducir a SQL, así que borrar a <b>cualquier</b> administrador daba error.
    /// </summary>
    [Fact]
    public async Task An_admin_can_be_deleted_if_others_remain()
    {
        var admin = await AdminAsync();

        var created = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            Name = "Administrador de paso",
            Email = $"admin.de.paso.{Guid.NewGuid():N}@acme.com",
            Password = MemberPassword,
            Role = "Admin"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var deleted = await admin.DeleteAsync($"/api/v1/users/{id}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// No hay forma anónima de conseguir un token.
    ///
    /// Existía <c>POST /auth/guest-token</c>: emitía un token con rol «Guest» a cualquiera que lo
    /// pidiera, y ese token pasaba todas las comprobaciones de «ha iniciado sesión» de la API.
    /// </summary>
    [Fact]
    public async Task No_anonymous_guest_tokens_are_issued()
    {
        var response = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/guest-token", new { TenantSlug = "acme" });

        response.IsSuccessStatusCode.Should().BeFalse();
        (await response.Content.ReadAsStringAsync()).Should().NotContain("accessToken");
    }
}
