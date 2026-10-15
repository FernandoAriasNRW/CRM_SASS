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
/// «Tickets por área»: el ejemplo pedido para el constructor de informes.
///
/// No se podía hacer porque los campos personalizados eran solo de tareas y proyectos, y el
/// catálogo del constructor no sabía nada de ellos. Ahora un ticket tiene campos, el catálogo los
/// ofrece para agrupar, y el informe agrupa por su valor.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TicketsByAreaFlowTests(CrmApiFactory factory)
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

    private static async Task<Guid> DefineAsync(HttpClient client, string name, string target, string[]? options)
    {
        var response = await client.PostAsJsonAsync("/api/v1/custom-fields", new
        {
            name, type = options is null ? "Text" : "Select", targetEntity = target,
            isRequired = false, options, position = 0, formula = (string?)null,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> TicketAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tickets", new { Title = title, Description = "", Priority = "Low" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> PreviewAsync(HttpClient client, string source, string groupBy)
        => await client.PostAsJsonAsync("/api/v1/reports/preview", new
        {
            definition = new { dataSource = source, groupBy, measure = "count", visualization = "table", filters = Array.Empty<object>() },
            title = "Por área",
        });

    [Fact]
    public async Task Tickets_can_be_grouped_by_an_area_field()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var areaName = "Área " + Guid.NewGuid().ToString("N")[..6];
        var area = await DefineAsync(admin, areaName, "Ticket", ["Ventas", "Soporte"]);

        var sales = await TicketAsync(admin, "Factura mal emitida");
        var support = await TicketAsync(admin, "No entra en la aplicación");
        var other = await TicketAsync(admin, "Otro soporte");
        foreach (var (ticket, value) in new[] { (sales, "Ventas"), (support, "Soporte"), (other, "Soporte") })
            (await admin.PutAsJsonAsync($"/api/v1/custom-fields/values/{area}/{ticket}", new { value })).EnsureSuccessStatusCode();

        var catalog = await admin.GetFromJsonAsync<JsonElement>("/api/v1/reports/catalog");
        var ticketFields = catalog.GetProperty("dataSources").EnumerateArray()
            .Single(s => s.GetProperty("key").GetString() == "Tickets").GetProperty("fields").EnumerateArray();
        ticketFields.Should().Contain(f => f.GetProperty("key").GetString() == $"custom:{area}"
                                          && f.GetProperty("name").GetString() == areaName);

        var response = await PreviewAsync(admin, "Tickets", $"custom:{area}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();

        preview.GetProperty("columns")[0].GetString().Should().Be(areaName, "la cabecera es el nombre del campo");
        var counts = preview.GetProperty("rows").EnumerateArray()
            .ToDictionary(r => r[0].GetString()!, r => int.Parse(r[1].GetString()!));
        counts["Ventas"].Should().Be(1);
        counts["Soporte"].Should().Be(2);
        counts.Should().ContainKey("(sin asignar)", "los tickets sin área van juntos, no desaparecen");
    }

    [Fact]
    public async Task A_task_field_does_not_group_tickets()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var taskField = await DefineAsync(admin, "Solo tareas " + Guid.NewGuid().ToString("N")[..6], "Task", null);

        (await PreviewAsync(admin, "Tickets", $"custom:{taskField}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Rellenar un campo es editar el elemento: quien solo puede verlo no lo cambia.</summary>
    [Fact]
    public async Task Whoever_only_reads_a_ticket_cannot_fill_its_fields()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var area = await DefineAsync(admin, "Área " + Guid.NewGuid().ToString("N")[..6], "Ticket", ["Ventas", "Soporte"]);
        var ticket = await TicketAsync(admin, "Ticket ajeno");

        var email = $"lectora.{Guid.NewGuid():N}@acme.com";
        var created = await admin.PostAsJsonAsync("/api/v1/users", new { Name = "Lectora", Email = email, Password, Role = "Member" });
        var readerId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var tenantId = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me")).GetProperty("tenantId").GetGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.EntityPermissions.Add(EntityPermission.CreateForUser(tenantId, readerId, "Ticket", ticket, "Read"));
            await db.SaveChangesAsync();
        }
        var reader = await LoginAsync(email, Password);

        (await reader.PutAsJsonAsync($"/api/v1/custom-fields/values/{area}/{ticket}", new { value = "Ventas" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Lo que cuenta un informe de tickets con estos filtros, sumando todos los grupos.</summary>
    private static async Task<int> CountAsync(HttpClient client, params object[] filters)
    {
        var response = await client.PostAsJsonAsync("/api/v1/reports/preview", new
        {
            definition = new { dataSource = "Tickets", groupBy = "status", measure = "count", visualization = "table", filters },
            title = "Filtrado por área",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rows").EnumerateArray()
            .Sum(r => int.Parse(r[1].GetString()!));
    }

    /// <summary>
    /// «Solo los del área Ventas». Se acota a un agente nuevo para que solo cuenten los tickets de
    /// esta prueba: uno de Ventas, otro de Soporte y otro sin área.
    /// </summary>
    [Fact]
    public async Task Tickets_can_be_filtered_by_an_area_field()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var area = await DefineAsync(admin, "Área " + Guid.NewGuid().ToString("N")[..6], "Ticket", ["Ventas", "Soporte"]);

        var agent = await admin.PostAsJsonAsync("/api/v1/users", new { Name = "Agente", Email = $"agente.{Guid.NewGuid():N}@acme.com", Password, Role = "Member" });
        var agentId = (await agent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var sales = await TicketAsync(admin, "De ventas");
        var support = await TicketAsync(admin, "De soporte");
        var none = await TicketAsync(admin, "Sin área");
        foreach (var ticket in new[] { sales, support, none })
            (await admin.PatchAsJsonAsync($"/api/v1/tickets/{ticket}", new { AssignedAgentId = agentId })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/custom-fields/values/{area}/{sales}", new { value = "Ventas" })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/v1/custom-fields/values/{area}/{support}", new { value = "Soporte" })).EnsureSuccessStatusCode();

        var mine = new { field = "agent", @operator = "is", value = agentId.ToString() };
        object Area(string op, string? value = null) => new { field = $"custom:{area}", @operator = op, value };

        (await CountAsync(admin, mine, Area("is", "ventas"))).Should().Be(1, "sin distinguir mayúsculas, como la base");
        (await CountAsync(admin, mine, Area("is_not", "Ventas"))).Should().Be(2, "«no es» incluye a los que no tienen área");
        (await CountAsync(admin, mine, Area("empty"))).Should().Be(1);
        (await CountAsync(admin, mine, Area("not_empty"))).Should().Be(2);
        (await CountAsync(admin, mine, Area("contains", "port"))).Should().Be(1);

        // El catálogo lo ofrece para filtrar, con sus opciones como valores.
        var catalog = await admin.GetFromJsonAsync<JsonElement>("/api/v1/reports/catalog");
        var field = catalog.GetProperty("dataSources").EnumerateArray()
            .Single(s => s.GetProperty("key").GetString() == "Tickets").GetProperty("fields").EnumerateArray()
            .Single(f => f.GetProperty("key").GetString() == $"custom:{area}");
        field.GetProperty("operators").EnumerateArray().Select(o => o.GetString())
            .Should().Contain(["is", "is_not", "contains", "empty", "not_empty"]);
        field.GetProperty("values").EnumerateArray().Select(v => v.GetString()).Should().Equal("Ventas", "Soporte");
    }
}
