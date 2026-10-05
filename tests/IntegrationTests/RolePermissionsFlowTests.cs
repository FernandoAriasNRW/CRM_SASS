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
public sealed class RolePermissionsFlowTests(CrmApiFactory factory)
{
    private const string Password = "Miembro2026!x";

    private async Task<HttpClient> ClientAsync(string email, string password)
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static Task<HttpResponseMessage> MemberLevelAsync(HttpClient admin, string entityType, string level)
        => admin.PostAsJsonAsync("/api/v1/permissions", new
        {
            TargetType = "Role",
            RoleName = "Member",
            Permissions = new[] { new { EntityType = entityType, EntityId = Guid.Empty, PermissionLevel = level } }
        });

    private static async Task<Guid> ATaskAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/tasks?pageSize=1");
        return page.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Lowering_members_to_view_only_stops_them_editing_tasks()
    {
        var admin = await ClientAsync("admin@acme.com", "admin123");

        var email = $"miembro.permisos.{Guid.NewGuid():N}@acme.com";
        (await admin.PostAsJsonAsync("/api/v1/users",
            new { Name = "Miembro con permisos", Email = email, Password = Password, Role = "Member" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var member = await ClientAsync(email, Password);
        var task = await ATaskAsync(admin);

        try
        {
            // Se manda en plural a propósito, como lo mandaba la pantalla: una pestaña abierta
            // desde antes del cambio no puede volver a crear una fila que no consulta nadie.
            (await MemberLevelAsync(admin, "Tasks", "View")).EnsureSuccessStatusCode();

            var edit = await member.PatchAsJsonAsync($"/api/v1/tasks/{task}", new { EstimatedHours = 3m });
            edit.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "el rol Member tiene sólo lectura sobre las tareas");

            (await MemberLevelAsync(admin, "Task", "Edit")).EnsureSuccessStatusCode();

            var editAgain = await member.PatchAsJsonAsync($"/api/v1/tasks/{task}", new { EstimatedHours = 3m });
            editAgain.StatusCode.Should().Be(HttpStatusCode.OK, await editAgain.Content.ReadAsStringAsync());
        }
        finally
        {
            await MemberLevelAsync(admin, "Task", "Edit");
        }
    }

    /// <summary>
    /// El plural y el singular acaban en la misma fila: guardar dos veces no deja dos niveles
    /// distintos para lo mismo, que es cómo la pantalla enseñaba uno y se aplicaba otro.
    /// </summary>
    [Fact]
    public async Task Saving_plural_or_singular_updates_the_same_row()
    {
        var admin = await ClientAsync("admin@acme.com", "admin123");

        try
        {
            (await MemberLevelAsync(admin, "Tasks", "View")).EnsureSuccessStatusCode();
            (await MemberLevelAsync(admin, "Task", "Full")).EnsureSuccessStatusCode();

            var rows = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/permissions?targetType=Role&roleName=Member"))
                .EnumerateArray()
                .Where(p => p.GetProperty("entityId").GetGuid() == Guid.Empty)
                .Where(p => p.GetProperty("entityType").GetString() is "Task" or "Tasks")
                .ToList();

            rows.Should().ContainSingle();
            rows[0].GetProperty("entityType").GetString().Should().Be("Task");
            rows[0].GetProperty("permissionLevel").GetString().Should().Be("Full");
        }
        finally
        {
            await MemberLevelAsync(admin, "Task", "Edit");
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
    public async Task View_only_stops_writing_projects_tickets_and_documents(string type)
    {
        var admin = await ClientAsync("admin@acme.com", "admin123");

        var email = $"miembro.{type.ToLowerInvariant()}.{Guid.NewGuid():N}@acme.com";
        (await admin.PostAsJsonAsync("/api/v1/users",
            new { Name = "Miembro sin escritura", Email = email, Password = Password, Role = "Member" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var member = await ClientAsync(email, Password);

        Guid? project = null;
        if (type == "Project")
        {
            var page = await admin.GetFromJsonAsync<JsonElement>("/api/v1/projects?pageSize=1");
            project = page.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        }

        Task<HttpResponseMessage> Write() => type switch
        {
            "Ticket" => member.PostAsJsonAsync("/api/v1/tickets",
                new { Title = "Ticket de la prueba de permisos", Description = "Creado por un miembro", Priority = "Medium" }),
            "Document" => member.PostAsJsonAsync("/api/v1/docs/",
                new { Title = "Documento de la prueba de permisos", Description = "Creado por un miembro", Type = 1 }),
            _ => member.PatchAsJsonAsync($"/api/v1/projects/{project}", new { Description = (string?)null })
        };

        try
        {
            (await MemberLevelAsync(admin, type, "View")).EnsureSuccessStatusCode();
            (await Write()).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await MemberLevelAsync(admin, type, "Edit")).EnsureSuccessStatusCode();
            var withPermission = await Write();
            withPermission.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, await withPermission.Content.ReadAsStringAsync());
            withPermission.IsSuccessStatusCode.Should().BeTrue(await withPermission.Content.ReadAsStringAsync());
        }
        finally
        {
            await MemberLevelAsync(admin, type, "Edit");
        }
    }
}
