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
public sealed class AdministracionSoloParaAdministradoresFlowTests(CrmApiFactory factory)
{
    private const string ContrasenaDeMiembro = "Miembro2026!x";

    private async Task<HttpClient> ClienteConTokenAsync(string email, string contrasena)
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = contrasena });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private Task<HttpClient> AdministradorAsync() => ClienteConTokenAsync("admin@acme.com", "admin123");

    /// <summary>Un miembro recién creado por el administrador, con su propia sesión.</summary>
    private async Task<(HttpClient cliente, Guid id)> MiembroAsync()
    {
        var admin = await AdministradorAsync();
        var email = $"miembro.{Guid.NewGuid():N}@acme.com";

        var creado = await admin.PostAsJsonAsync("/api/v1/users",
            new { Name = "Miembro de prueba", Email = email, Password = ContrasenaDeMiembro, Role = "Member" });
        creado.StatusCode.Should().Be(HttpStatusCode.Created, await creado.Content.ReadAsStringAsync());

        var id = (await creado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (await ClienteConTokenAsync(email, ContrasenaDeMiembro), id);
    }

    [Fact]
    public async Task Un_miembro_no_puede_crear_un_administrador()
    {
        var (miembro, _) = await MiembroAsync();

        var respuesta = await miembro.PostAsJsonAsync("/api/v1/users", new
        {
            Name = "Colado",
            Email = $"colado.{Guid.NewGuid():N}@acme.com",
            Password = ContrasenaDeMiembro,
            Role = "Admin"
        });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>La otra mitad de la misma escalada: cambiarse el rol a sí mismo.</summary>
    [Fact]
    public async Task Un_miembro_no_puede_cambiarse_el_rol()
    {
        var (miembro, id) = await MiembroAsync();

        var respuesta = await miembro.PutAsJsonAsync($"/api/v1/users/{id}",
            new { Name = "Miembro de prueba", Email = (string?)null, Role = "Admin" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_miembro_no_puede_borrar_personas()
    {
        var (miembro, _) = await MiembroAsync();
        var (_, otro) = await MiembroAsync();

        var respuesta = await miembro.DeleteAsync($"/api/v1/users/{otro}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Ni leer la matriz de permisos ni, sobre todo, darse permisos.</summary>
    [Fact]
    public async Task Un_miembro_no_puede_ver_ni_cambiar_permisos()
    {
        var (miembro, id) = await MiembroAsync();

        var lectura = await miembro.GetAsync("/api/v1/permissions?targetType=Role");
        lectura.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var escritura = await miembro.PostAsJsonAsync("/api/v1/permissions", new
        {
            TargetType = "User",
            UserId = id,
            Permissions = new[] { new { EntityType = "Settings", EntityId = Guid.Empty, PermissionLevel = "Full" } }
        });
        escritura.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Lo que un miembro sí necesita sigue abierto: el directorio de personas, que usan las
    /// menciones y los selectores de responsable, y su propio perfil.
    /// </summary>
    [Fact]
    public async Task Un_miembro_sigue_viendo_el_directorio_y_su_perfil()
    {
        var (miembro, _) = await MiembroAsync();

        (await miembro.GetAsync("/api/v1/users/tenant")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await miembro.GetAsync("/api/v1/auth/users/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task El_administrador_sigue_gestionando_personas_y_permisos()
    {
        var admin = await AdministradorAsync();

        (await admin.GetAsync("/api/v1/permissions?targetType=Role")).StatusCode.Should().Be(HttpStatusCode.OK);

        var (_, id) = await MiembroAsync();
        var cambio = await admin.PutAsJsonAsync($"/api/v1/users/{id}",
            new { Name = "Miembro renombrado", Email = (string?)null, Role = "Member" });
        cambio.StatusCode.Should().Be(HttpStatusCode.OK, await cambio.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Borrar a un administrador, habiendo otros, funciona.
    ///
    /// Salió al limpiar los datos de la sonda: la cuenta de administradores que protege al último
    /// no se podía traducir a SQL, así que borrar a <b>cualquier</b> administrador daba error.
    /// </summary>
    [Fact]
    public async Task Se_puede_borrar_un_administrador_si_quedan_otros()
    {
        var admin = await AdministradorAsync();

        var creado = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            Name = "Administrador de paso",
            Email = $"admin.de.paso.{Guid.NewGuid():N}@acme.com",
            Password = ContrasenaDeMiembro,
            Role = "Admin"
        });
        creado.StatusCode.Should().Be(HttpStatusCode.Created, await creado.Content.ReadAsStringAsync());
        var id = (await creado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var borrado = await admin.DeleteAsync($"/api/v1/users/{id}");

        borrado.StatusCode.Should().Be(HttpStatusCode.NoContent, await borrado.Content.ReadAsStringAsync());
    }
}
