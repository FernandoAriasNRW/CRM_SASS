using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Un permiso por rol cambia de verdad lo que se puede hacer.
///
/// No lo hacía. La pantalla de permisos guardaba «Tasks» y los comandos preguntaban por «Task»:
/// la fila existía, se veía en la pantalla con su nivel, y ninguna comprobación llegaba a leerla.
/// Bajar a los miembros a «sólo ver» no les impedía editar nada.
///
/// Por eso la prueba no mira la tabla: pide el cambio por la API, como la pantalla, y comprueba el
/// efecto sobre una tarea real.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PermisosPorRolFlowTests(CrmApiFactory factory)
{
    private const string Contrasena = "Miembro2026!x";

    private async Task<HttpClient> ClienteAsync(string email, string contrasena)
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = contrasena });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var cliente = factory.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static Task<HttpResponseMessage> NivelDeMiembrosAsync(HttpClient admin, string entityType, string nivel)
        => admin.PostAsJsonAsync("/api/v1/permissions", new
        {
            TargetType = "Role",
            RoleName = "Member",
            Permissions = new[] { new { EntityType = entityType, EntityId = Guid.Empty, PermissionLevel = nivel } }
        });

    private static async Task<Guid> UnaTareaAsync(HttpClient cliente)
    {
        var pagina = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/tasks?pageSize=1");
        return pagina.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Bajar_a_los_miembros_a_solo_ver_les_impide_editar_tareas()
    {
        var admin = await ClienteAsync("admin@acme.com", "admin123");

        var email = $"miembro.permisos.{Guid.NewGuid():N}@acme.com";
        (await admin.PostAsJsonAsync("/api/v1/users",
            new { Name = "Miembro con permisos", Email = email, Password = Contrasena, Role = "Member" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var miembro = await ClienteAsync(email, Contrasena);
        var tarea = await UnaTareaAsync(admin);

        try
        {
            // Se manda en plural a propósito, como lo mandaba la pantalla: una pestaña abierta
            // desde antes del cambio no puede volver a crear una fila que no consulta nadie.
            (await NivelDeMiembrosAsync(admin, "Tasks", "View")).EnsureSuccessStatusCode();

            var editar = await miembro.PatchAsJsonAsync($"/api/v1/tasks/{tarea}", new { EstimatedHours = 3m });
            editar.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "el rol Member tiene sólo lectura sobre las tareas");

            (await NivelDeMiembrosAsync(admin, "Task", "Edit")).EnsureSuccessStatusCode();

            var editarDeNuevo = await miembro.PatchAsJsonAsync($"/api/v1/tasks/{tarea}", new { EstimatedHours = 3m });
            editarDeNuevo.StatusCode.Should().Be(HttpStatusCode.OK, await editarDeNuevo.Content.ReadAsStringAsync());
        }
        finally
        {
            await NivelDeMiembrosAsync(admin, "Task", "Edit");
        }
    }

    /// <summary>
    /// El plural y el singular acaban en la misma fila: guardar dos veces no deja dos niveles
    /// distintos para lo mismo, que es cómo la pantalla enseñaba uno y se aplicaba otro.
    /// </summary>
    [Fact]
    public async Task Guardar_en_plural_o_en_singular_actualiza_la_misma_fila()
    {
        var admin = await ClienteAsync("admin@acme.com", "admin123");

        try
        {
            (await NivelDeMiembrosAsync(admin, "Tasks", "View")).EnsureSuccessStatusCode();
            (await NivelDeMiembrosAsync(admin, "Task", "Full")).EnsureSuccessStatusCode();

            var filas = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/permissions?targetType=Role&roleName=Member"))
                .EnumerateArray()
                .Where(p => p.GetProperty("entityId").GetGuid() == Guid.Empty)
                .Where(p => p.GetProperty("entityType").GetString() is "Task" or "Tasks")
                .ToList();

            filas.Should().ContainSingle();
            filas[0].GetProperty("entityType").GetString().Should().Be("Task");
            filas[0].GetProperty("permissionLevel").GetString().Should().Be("Full");
        }
        finally
        {
            await NivelDeMiembrosAsync(admin, "Task", "Edit");
        }
    }

    /// <summary>
    /// Proyectos, tickets y documentos también obedecen al nivel por rol.
    ///
    /// Hasta ahora sólo las tareas pedían autorización: en los otros tres módulos el nivel se
    /// guardaba desde la pantalla y ningún comando lo consultaba.
    /// </summary>
    [Theory]
    [InlineData("Ticket")]
    [InlineData("Document")]
    [InlineData("Project")]
    public async Task Solo_ver_impide_escribir_en_proyectos_tickets_y_documentos(string tipo)
    {
        var admin = await ClienteAsync("admin@acme.com", "admin123");

        var email = $"miembro.{tipo.ToLowerInvariant()}.{Guid.NewGuid():N}@acme.com";
        (await admin.PostAsJsonAsync("/api/v1/users",
            new { Name = "Miembro sin escritura", Email = email, Password = Contrasena, Role = "Member" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var miembro = await ClienteAsync(email, Contrasena);

        Guid? proyecto = null;
        if (tipo == "Project")
        {
            var pagina = await admin.GetFromJsonAsync<JsonElement>("/api/v1/projects?pageSize=1");
            proyecto = pagina.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        }

        Task<HttpResponseMessage> Escribir() => tipo switch
        {
            "Ticket" => miembro.PostAsJsonAsync("/api/v1/tickets",
                new { Title = "Ticket de la prueba de permisos", Description = "Creado por un miembro", Priority = "Medium" }),
            "Document" => miembro.PostAsJsonAsync("/api/v1/docs/",
                new { Title = "Documento de la prueba de permisos", Description = "Creado por un miembro", Type = 1 }),
            _ => miembro.PatchAsJsonAsync($"/api/v1/projects/{proyecto}", new { Description = (string?)null })
        };

        try
        {
            (await NivelDeMiembrosAsync(admin, tipo, "View")).EnsureSuccessStatusCode();
            (await Escribir()).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await NivelDeMiembrosAsync(admin, tipo, "Edit")).EnsureSuccessStatusCode();
            var conPermiso = await Escribir();
            conPermiso.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, await conPermiso.Content.ReadAsStringAsync());
            conPermiso.IsSuccessStatusCode.Should().BeTrue(await conPermiso.Content.ReadAsStringAsync());
        }
        finally
        {
            await NivelDeMiembrosAsync(admin, tipo, "Edit");
        }
    }
}
