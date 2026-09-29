using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ApiHost.Tags;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tags.Domain.Entities;
using Tags.Domain.ValueObjects;
using Tags.Infrastructure.Persistence;
using Ticketing.Infrastructure.Persistence;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Tareas, tickets, proyectos, informes y dashboards etiquetados con las etiquetas del módulo Tags.
///
/// Antes cada uno tenía su columna <c>TagIds</c> y nadie la rellenaba: la ficha de tarea mandaba
/// claves fijas que se descartaban sin error, y los tickets guardaban esas claves como texto. Ahora
/// todos guardan ids de etiquetas de verdad, se comprueba que sean de la organización, y borrar
/// una etiqueta la suelta de todos.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TaggingFlowTests(CrmApiFactory factory)
{
    private sealed record Session(HttpClient Client, Guid TenantId);

    private async Task<Session> AdminAsync()
    {
        var login = await factory.CreateClient()
            .PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me");
        return new Session(client, me.GetProperty("tenantId").GetGuid());
    }

    private static async Task<Guid> NewTagAsync(Session admin)
    {
        var response = await admin.Client.PostAsJsonAsync("/api/v1/tags", new { name = $"Etiquetado {Guid.NewGuid():N}", category = "WorkType" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> BuiltInTagAsync(Session admin, string key)
    {
        var tags = await admin.Client.GetFromJsonAsync<JsonElement>("/api/v1/tags");
        return tags.EnumerateArray().Single(t => t.GetProperty("builtInKey").GetString() == key).GetProperty("id").GetGuid();
    }

    /// <summary>Una etiqueta de otra organización, escrita directamente en la base.</summary>
    private async Task<Guid> ForeignTagAsync()
    {
        var tenant = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TagsDbContext>();
        using var _ = db.AsTenant(tenant);
        var tag = Tag.Create(tenant, $"Ajena {Guid.NewGuid():N}", "#000000", TagCategory.Business);
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag.Id;
    }

    private static IEnumerable<Guid> TagIdsOf(JsonElement element)
        => element.GetProperty("tagIds").EnumerateArray().Select(t => t.GetGuid());

    private static async Task<Guid> AnyIdAsync(Session admin, string url)
    {
        var page = await admin.Client.GetFromJsonAsync<JsonElement>(url);
        var items = page.ValueKind == JsonValueKind.Array ? page : page.GetProperty("items");
        return items.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private static async Task<Guid> NewTicketAsync(Session admin)
    {
        var response = await admin.Client.PostAsJsonAsync("/api/v1/tickets",
            new { Title = $"Ticket etiquetado {Guid.NewGuid():N}", Description = "Para etiquetar", Priority = "Low" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> NewReportAsync(Session admin)
    {
        var response = await admin.Client.PostAsJsonAsync("/api/v1/reports",
            new { name = $"Informe etiquetado {Guid.NewGuid():N}", type = "TaskSummary", format = "Csv" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.ValueKind == JsonValueKind.Object ? body.GetProperty("id").GetGuid() : body.GetGuid();
    }

    private static async Task<Guid> NewDashboardAsync(Session admin, params Guid[] tagIds)
    {
        var response = await admin.Client.PostAsJsonAsync("/api/v1/dashboards", new
        {
            title = $"Panel etiquetado {Guid.NewGuid():N}", isDefault = false, isPublic = false, widgetsJson = "[]", tagIds,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetGuid();
    }

    private static async Task<JsonElement> DashboardAsync(Session admin, Guid id)
        => (await admin.Client.GetFromJsonAsync<JsonElement>("/api/v1/dashboards"))
            .EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == id);

    // ── Tareas ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_task_saves_its_tags_and_an_omitted_list_leaves_them_alone()
    {
        var admin = await AdminAsync();
        var task = await AnyIdAsync(admin, "/api/v1/tasks?pageSize=1");
        var feature = await BuiltInTagAsync(admin, "feature");
        var own = await NewTagAsync(admin);

        var saved = await admin.Client.PatchAsJsonAsync($"/api/v1/tasks/{task}", new { tagIds = new[] { feature, own } });
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        TagIdsOf(await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}")).Should().Equal(feature, own);

        (await admin.Client.PatchAsJsonAsync($"/api/v1/tasks/{task}", new { estimatedHours = 2m })).EnsureSuccessStatusCode();
        TagIdsOf(await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}"))
            .Should().Equal([feature, own], "un PATCH sin tagIds no toca las etiquetas");

        (await admin.Client.PatchAsJsonAsync($"/api/v1/tasks/{task}", new { tagIds = Array.Empty<Guid>() })).EnsureSuccessStatusCode();
        TagIdsOf(await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}")).Should().BeEmpty();
    }

    [Fact]
    public async Task A_task_rejects_tags_that_do_not_exist_or_belong_to_another_organization()
    {
        var admin = await AdminAsync();
        var task = await AnyIdAsync(admin, "/api/v1/tasks?pageSize=1");

        foreach (var tag in new[] { Guid.NewGuid(), await ForeignTagAsync() })
        {
            var response = await admin.Client.PatchAsJsonAsync($"/api/v1/tasks/{task}", new { tagIds = new[] { tag } });
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain(tag.ToString());
        }
    }

    // ── Tickets, proyectos, informes y dashboards ───────────────────────────────────────────

    [Fact]
    public async Task A_ticket_saves_its_tags_and_rejects_unknown_ones()
    {
        var admin = await AdminAsync();
        var ticket = await NewTicketAsync(admin);
        var billing = await BuiltInTagAsync(admin, "billing");

        (await admin.Client.PatchAsJsonAsync($"/api/v1/tickets/{ticket}", new { tagIds = new[] { billing } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        TagIdsOf(await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{ticket}")).Should().Equal(billing);

        (await admin.Client.PatchAsJsonAsync($"/api/v1/tickets/{ticket}", new { tagIds = new[] { Guid.NewGuid() } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_project_saves_its_tags_and_rejects_unknown_ones()
    {
        var admin = await AdminAsync();
        var project = await AnyIdAsync(admin, "/api/v1/projects?pageSize=1");
        var milestone = await BuiltInTagAsync(admin, "mvp");

        (await admin.Client.PatchAsJsonAsync($"/api/v1/projects/{project}", new { tagIds = new[] { milestone } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        TagIdsOf(await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{project}")).Should().Contain(milestone);

        (await admin.Client.PatchAsJsonAsync($"/api/v1/projects/{project}", new { tagIds = new[] { Guid.NewGuid() } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "una etiqueta desconocida no es un proyecto que no existe");
    }

    [Fact]
    public async Task A_report_saves_its_tags_with_its_own_endpoint()
    {
        var admin = await AdminAsync();
        var report = await NewReportAsync(admin);
        var tag = await NewTagAsync(admin);

        (await admin.Client.PutAsJsonAsync($"/api/v1/reports/{report}/tags", new { tagIds = new[] { tag } }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        TagIdsOf(await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{report}")).Should().Equal(tag);

        (await admin.Client.PutAsJsonAsync($"/api/v1/reports/{report}/tags", new { tagIds = new[] { Guid.NewGuid() } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Client.PutAsJsonAsync($"/api/v1/reports/{Guid.NewGuid()}/tags", new { tagIds = new[] { tag } }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_dashboard_checks_its_tags_when_created()
    {
        var admin = await AdminAsync();
        var tag = await NewTagAsync(admin);

        var dashboard = await NewDashboardAsync(admin, tag);
        TagIdsOf(await DashboardAsync(admin, dashboard)).Should().Equal(tag);

        var rejected = await admin.Client.PostAsJsonAsync("/api/v1/dashboards", new
        {
            title = "Con etiqueta inventada", isDefault = false, isPublic = false, widgetsJson = "[]", tagIds = new[] { Guid.NewGuid() },
        });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Borrar una etiqueta la suelta de todo ───────────────────────────────────────────────

    [Fact]
    public async Task Deleting_a_tag_removes_it_from_everything_that_carried_it()
    {
        var admin = await AdminAsync();
        var tag = await NewTagAsync(admin);
        var keep = await NewTagAsync(admin);

        var task = await AnyIdAsync(admin, "/api/v1/tasks?pageSize=1");
        var ticket = await NewTicketAsync(admin);
        var project = await AnyIdAsync(admin, "/api/v1/projects?pageSize=1");
        var report = await NewReportAsync(admin);

        (await admin.Client.PatchAsJsonAsync($"/api/v1/tasks/{task}", new { tagIds = new[] { tag, keep } })).EnsureSuccessStatusCode();
        (await admin.Client.PatchAsJsonAsync($"/api/v1/tickets/{ticket}", new { tagIds = new[] { tag, keep } })).EnsureSuccessStatusCode();
        (await admin.Client.PatchAsJsonAsync($"/api/v1/projects/{project}", new { tagIds = new[] { tag, keep } })).EnsureSuccessStatusCode();
        (await admin.Client.PutAsJsonAsync($"/api/v1/reports/{report}/tags", new { tagIds = new[] { tag, keep } })).EnsureSuccessStatusCode();
        var dashboard = await NewDashboardAsync(admin, tag, keep);

        (await admin.Client.DeleteAsync($"/api/v1/tags/{tag}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        TagIdsOf(await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tasks/{task}")).Should().Equal(keep);
        TagIdsOf(await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{ticket}")).Should().Equal(keep);
        TagIdsOf(await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/projects/{project}")).Should().Equal(keep);
        TagIdsOf(await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{report}")).Should().Equal(keep);
        TagIdsOf(await DashboardAsync(admin, dashboard)).Should().Equal(keep);
    }

    // ── Soporte y la conversión de las claves antiguas de los tickets ───────────────────────

    [Fact]
    public async Task Support_is_a_built_in_category_with_its_tags()
    {
        var admin = await AdminAsync();

        var categories = await admin.Client.GetFromJsonAsync<JsonElement>("/api/v1/tags/categories");
        categories.EnumerateArray().Single(c => c.GetProperty("name").GetString() == "Support")
            .GetProperty("label").GetString().Should().Be("Soporte");

        var tags = await admin.Client.GetFromJsonAsync<JsonElement>("/api/v1/tags?language=en");
        tags.EnumerateArray().Single(t => t.GetProperty("builtInKey").GetString() == "waiting-client")
            .GetProperty("name").GetString().Should().Be("Waiting on customer");
    }

    [Fact]
    public async Task Legacy_ticket_keys_become_real_tags_and_urgent_is_dropped()
    {
        var admin = await AdminAsync();
        var ticket = await NewTicketAsync(admin);

        // Lo que guardaba la ficha antes de este cambio: claves fijas separadas por comas.
        using (var scope = factory.Services.CreateScope())
        {
            var ticketing = scope.ServiceProvider.GetRequiredService<TicketingDbContext>();
            await ticketing.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE `Tickets` SET `Tags` = 'billing,urgent,feature-request' WHERE `Id` = {ticket}");
        }

        using (var scope = factory.Services.CreateScope())
        {
            var converter = scope.ServiceProvider.GetRequiredService<LegacyTicketTagsConverter>();
            (await converter.ConvertAsync()).Should().BeGreaterThan(0);
            (await converter.ConvertAsync()).Should().Be(0, "la conversión vacía el texto, así que es idempotente");
        }

        TagIdsOf(await admin.Client.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{ticket}"))
            .Should().Equal(await BuiltInTagAsync(admin, "billing"), await BuiltInTagAsync(admin, "feature"));
    }
}
