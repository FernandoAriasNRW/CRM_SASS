using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Barras apiladas: cada barra partida por un segundo campo.
///
/// «Barras apiladas» se ofrecía y se pintaba como barras normales, porque el catálogo no tenía
/// segunda dimensión. Ahora la definición lleva <c>stackBy</c> y el informe sale con una columna
/// por cada trozo de la barra.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class StackedBarsFlowTests(CrmApiFactory factory)
{
    private async Task<HttpClient> AdminAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static Task<HttpResponseMessage> PreviewAsync(HttpClient client, object definition)
        => client.PostAsJsonAsync("/api/v1/reports/preview", new { definition, title = "Apiladas" });

    [Fact]
    public async Task Tickets_by_status_stacked_by_priority()
    {
        var admin = await AdminAsync();
        var agent = await admin.PostAsJsonAsync("/api/v1/users", new { Name = "Agente", Email = $"agente.{Guid.NewGuid():N}@acme.com", Password = "Miembro2026!x", Role = "Member" });
        var agentId = (await agent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        foreach (var priority in new[] { "Low", "High", "High" })
        {
            var created = await admin.PostAsJsonAsync("/api/v1/tickets", new { Title = "Apilado " + priority, Description = "", Priority = priority });
            var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            (await admin.PatchAsJsonAsync($"/api/v1/tickets/{id}", new { AssignedAgentId = agentId })).EnsureSuccessStatusCode();
        }

        var response = await PreviewAsync(admin, new
        {
            dataSource = "Tickets", groupBy = "status", stackBy = "priority", measure = "count", visualization = "stacked_bar",
            filters = new[] { new { field = "agent", @operator = "is", value = agentId.ToString() } },
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Una columna por trozo, de mayor a menor: dos «High» y un «Low».
        preview.GetProperty("columns").EnumerateArray().Select(c => c.GetString())
            .Should().Equal("Estado", "High", "Low");
        var open = preview.GetProperty("rows").EnumerateArray().Single(r => r[0].GetString() == "Open");
        open.EnumerateArray().Select(c => c.GetString()).Should().Equal("Open", "2", "1");
        preview.GetProperty("subtitle").GetString().Should().Contain("apilado por prioridad");
    }

    /// <summary>Una definición guardada como apilada antes de que existiera stackBy sigue funcionando.</summary>
    [Fact]
    public async Task A_stacked_report_without_a_second_field_is_plain_bars()
    {
        var admin = await AdminAsync();

        var response = await PreviewAsync(admin, new { dataSource = "Tickets", groupBy = "status", measure = "count", visualization = "stacked_bar" });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("columns").GetArrayLength().Should().Be(2);
    }

    [Theory]
    [InlineData("bar", "priority")]            // solo se apila en las barras apiladas
    [InlineData("stacked_bar", "status")]      // por el mismo campo no se parte nada
    [InlineData("stacked_bar", "created_at")]  // por una fecha no
    [InlineData("stacked_bar", "no_existe")]
    public async Task A_second_field_that_makes_no_sense_is_rejected(string visualization, string stackBy)
    {
        var admin = await AdminAsync();

        var response = await PreviewAsync(admin, new { dataSource = "Tickets", groupBy = "status", stackBy, measure = "count", visualization });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
