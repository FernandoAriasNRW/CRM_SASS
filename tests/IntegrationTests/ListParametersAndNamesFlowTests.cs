using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Los informes enseñan nombres de persona, y las listas de informes y proyectos hacen caso de lo
/// que manda la pantalla.
///
/// <list type="bullet">
/// <item>Agrupar por responsable y el informe de actividad por persona escribían el Guid.</item>
/// <item><c>/reports</c> ignoraba la búsqueda, el formato y el orden; filtrar por tipo reventaba,
/// porque comparaba con una propiedad calculada; y el total salía mal, con la página en su sitio.</item>
/// <item><c>/projects</c> ignoraba el orden y las fechas «Desde» y «Hasta».</item>
/// </list>
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ListParametersAndNamesFlowTests(CrmApiFactory factory)
{
    private async Task<(HttpClient Client, Guid Me, string Name)> AdminAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = "admin@acme.com", Password = "admin123" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/users/me");
        return (client, me.GetProperty("id").GetGuid(), me.GetProperty("name").GetString()!);
    }

    private static async Task TaskForAsync(HttpClient client, Guid assignee)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tasks", new
        {
            projectId = await TestProjects.CreateAsync(client),
            title = "Tarea para el informe " + Guid.NewGuid().ToString("N")[..6],
            description = "",
            assigneeId = assignee,
            estimatedHours = 2m,
            dueDate = "2026-12-01",
            priority = "Normal",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> ReportAsync(HttpClient client, string name, string type, string format)
    {
        var response = await client.PostAsJsonAsync("/api/v1/reports", new { name, type, format });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<List<string>> ReportNamesAsync(HttpClient client, string query)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports?{query}");
        return page.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("name").GetString()!).ToList();
    }

    [Fact]
    public async Task Grouping_by_assignee_shows_names_not_ids()
    {
        var (client, me, myName) = await AdminAsync();
        await TaskForAsync(client, me);

        var response = await client.PostAsJsonAsync("/api/v1/reports/preview", new
        {
            definition = new
            {
                dataSource = "Tasks", groupBy = "assignee", measure = "count", visualization = "table",
                filters = new[] { new { field = "assignee", @operator = "is", value = me.ToString() } },
            },
            title = "Tareas por responsable",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var groups = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rows").EnumerateArray()
            .Select(r => r[0].GetString()).ToList();
        groups.Should().Equal(myName);
    }

    [Fact]
    public async Task The_activity_report_names_each_person()
    {
        var (client, me, myName) = await AdminAsync();
        await TaskForAsync(client, me);
        var reportId = await ReportAsync(client, "Actividad " + Guid.NewGuid().ToString("N")[..6], "UserActivity", "Csv");

        var request = await client.PostAsync($"/api/v1/reports/{reportId}/export?format=Csv", null);
        var exportId = (await request.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var deadline = DateTime.UtcNow.AddSeconds(20);
        JsonElement status;
        do
        {
            status = await client.GetFromJsonAsync<JsonElement>($"/api/v1/exports/{exportId}");
            if (status.GetProperty("status").GetString() is "Ready" or "Failed") break;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);
        status.GetProperty("status").GetString().Should().Be("Ready");

        var csv = await client.GetStringAsync($"/api/v1/exports/{exportId}/download");
        csv.Should().Contain(myName).And.NotContain(me.ToString());
    }

    [Fact]
    public async Task The_report_list_searches_filters_sorts_and_counts()
    {
        var (client, _, _) = await AdminAsync();
        var prefix = "Lista " + Guid.NewGuid().ToString("N")[..6];
        await ReportAsync(client, prefix + " A", "TaskBreakdown", "Pdf");
        await ReportAsync(client, prefix + " B", "KpiSummary", "Csv");
        var search = Uri.EscapeDataString(prefix);

        var firstPage = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports?search={search}&pageSize=1");
        firstPage.GetProperty("totalCount").GetInt32().Should().Be(2, "el total no es el número de página");
        firstPage.GetProperty("items").EnumerateArray().Should().HaveCount(1);

        (await ReportNamesAsync(client, $"search={search}&type=TaskBreakdown")).Should().Equal(prefix + " A");
        (await ReportNamesAsync(client, $"search={search}&format=Csv")).Should().Equal(prefix + " B");
        (await ReportNamesAsync(client, $"search={search}&type=NoExiste")).Should().BeEmpty();

        (await ReportNamesAsync(client, $"search={search}&sortColumn=name&sortDirection=desc")).Should().Equal(prefix + " B", prefix + " A");
        (await ReportNamesAsync(client, $"search={search}&sortColumn=name&sortDirection=asc")).Should().Equal(prefix + " A", prefix + " B");
    }

    [Fact]
    public async Task The_project_list_sorts_and_filters_by_dates()
    {
        var (client, _, _) = await AdminAsync();
        var prefix = "Orden " + Guid.NewGuid().ToString("N")[..6];
        await TestProjects.CreateAsync(client, prefix + " A");
        await TestProjects.CreateAsync(client, prefix + " B");
        var search = Uri.EscapeDataString(prefix);

        async Task<List<string>> NamesAsync(string query)
        {
            var page = await client.GetFromJsonAsync<JsonElement>($"/api/v1/projects?search={search}&{query}");
            return page.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToList();
        }

        (await NamesAsync("sortColumn=name&sortDirection=desc")).Should().Equal(prefix + " B", prefix + " A");
        (await NamesAsync("sortColumn=name&sortDirection=asc")).Should().Equal(prefix + " A", prefix + " B");

        var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var lastMonth = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
        (await NamesAsync($"startDate={tomorrow}")).Should().BeEmpty("empezaron hoy");
        (await NamesAsync($"startDate={lastMonth}&endDate={tomorrow}")).Should().HaveCount(2);
    }

    /// <summary>
    /// El proyecto recién creado sale el primero en el orden por defecto.
    ///
    /// Todos los creados hoy empiezan hoy, y entre ellos el listado desempataba por el
    /// identificador, que es aleatorio: con muchos proyectos del mismo día, el nuevo podía quedar
    /// en la segunda página. Ahora desempata la hora de alta, del más reciente al más antiguo.
    /// </summary>
    [Fact]
    public async Task A_new_project_comes_first_in_the_default_order()
    {
        var (client, _, _) = await AdminAsync();

        for (var i = 0; i < 3; i++)
        {
            var name = "Recién creado " + Guid.NewGuid().ToString("N")[..8];
            await TestProjects.CreateAsync(client, name);

            var first = await client.GetFromJsonAsync<JsonElement>("/api/v1/projects?pageSize=1");
            first.GetProperty("items")[0].GetProperty("name").GetString().Should().Be(name);
        }
    }
}
