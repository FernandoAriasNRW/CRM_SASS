using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// La zona horaria de la organización: la cambia quien administra y la usan los informes.
///
/// Antes los informes programados comparaban con la hora del servidor y los exportados ponían
/// «UTC» en todas las fechas, fuera cual fuera el país de la organización.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OrganizationTimeZoneFlowTests(CrmApiFactory factory)
{
    private const string Settings = "/api/v1/organization/settings";
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

    private static async Task<string?> ZoneAsync(HttpClient client)
        => (await client.GetFromJsonAsync<JsonElement>(Settings)).GetProperty("timeZone").GetString();

    [Fact]
    public async Task Only_an_admin_changes_it_and_only_to_a_real_zone()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var email = $"zona.{Guid.NewGuid():N}@acme.com";
        (await admin.PostAsJsonAsync("/api/v1/users", new { Name = "Zona", Email = email, Password = MemberPassword, Role = "Member" }))
            .EnsureSuccessStatusCode();
        var member = await LoginAsync(email, MemberPassword);

        try
        {
            (await admin.PutAsJsonAsync(Settings, new { timeZone = "America/Bogota" })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await ZoneAsync(member)).Should().Be("America/Bogota", "leerla puede cualquiera de la organización");

            (await member.PutAsJsonAsync(Settings, new { timeZone = "Europe/Madrid" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await admin.PutAsJsonAsync(Settings, new { timeZone = "Romance Standard Time" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ZoneAsync(admin)).Should().Be("America/Bogota");
        }
        finally
        {
            await admin.PutAsJsonAsync(Settings, new { timeZone = "UTC" });
        }
    }

    /// <summary>
    /// El informe dice la hora en la zona de la organización. Se mira en el subtítulo de la vista
    /// previa, que sale del mismo motor que los recuadros del panel y los informes a medida.
    /// </summary>
    [Fact]
    public async Task A_report_says_its_time_in_the_organization_zone()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");

        try
        {
            (await admin.PutAsJsonAsync(Settings, new { timeZone = "America/Bogota" })).EnsureSuccessStatusCode();

            var response = await admin.PostAsJsonAsync("/api/v1/reports/preview", new
            {
                definition = new { dataSource = "Tasks", groupBy = "status", measure = "count", visualization = "table", filters = Array.Empty<object>() },
                title = "Tareas por estado",
            });
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

            var subtitle = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("subtitle").GetString();
            subtitle.Should().Contain("(America/Bogota)").And.NotContain(" UTC");

            // Y la hora es la de Bogotá, no la del servidor.
            var bogota = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"));
            subtitle.Should().Contain(bogota.ToString("dd/MM/yyyy"));
        }
        finally
        {
            await admin.PutAsJsonAsync(Settings, new { timeZone = "UTC" });
        }
    }

    /// <summary>
    /// «Creado hoy» es hoy en la organización.
    ///
    /// Se elige una zona cuya fecha de hoy no es la de UTC en este momento —UTC+14 a partir de
    /// las 10:00 UTC, UTC-12 antes—, así la ventana del día local y la del día UTC no se solapan
    /// en el instante actual. Un ticket recién creado entra en la primera y no en la segunda. El
    /// ticket se asigna a un agente nuevo para que el filtro solo pueda contar ese.
    /// </summary>
    [Fact]
    public async Task A_date_filter_means_that_day_in_the_organization_zone()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var agentEmail = $"agente.{Guid.NewGuid():N}@acme.com";
        var agent = await admin.PostAsJsonAsync("/api/v1/users", new { Name = "Agente", Email = agentEmail, Password = MemberPassword, Role = "Member" });
        agent.EnsureSuccessStatusCode();
        var agentId = (await agent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var ticket = await admin.PostAsJsonAsync("/api/v1/tickets", new { Title = "Ticket de hoy", Description = "", Priority = "Low" });
        ticket.EnsureSuccessStatusCode();
        var ticketId = (await ticket.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await admin.PatchAsJsonAsync($"/api/v1/tickets/{ticketId}", new { AssignedAgentId = agentId })).EnsureSuccessStatusCode();

        var zoneName = DateTime.UtcNow.Hour >= 10 ? "Pacific/Kiritimati" : "Etc/GMT+12";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneName);
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
        DateTime.UtcNow.Date.Should().NotBe(localToday, "la prueba necesita una zona donde hoy sea otro día");

        try
        {
            (await admin.PutAsJsonAsync(Settings, new { timeZone = zoneName })).EnsureSuccessStatusCode();

            var response = await admin.PostAsJsonAsync("/api/v1/reports/preview", new
            {
                definition = new
                {
                    dataSource = "Tickets", groupBy = "status", measure = "count", visualization = "table",
                    filters = new object[]
                    {
                        new { field = "agent", @operator = "is", value = agentId.ToString() },
                        new { field = "created_at", @operator = "greater_than", value = localToday.ToString("yyyy-MM-dd") },
                        new { field = "created_at", @operator = "less_than", value = localToday.AddDays(1).ToString("yyyy-MM-dd") },
                    },
                },
                title = "Tickets de hoy",
            });
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

            var rows = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rows").EnumerateArray().ToList();
            rows.Sum(r => int.Parse(r[1].GetString()!)).Should().Be(1,
                $"el ticket se creó hoy en {zoneName}; leído en UTC sería otro día y no contaría");
        }
        finally
        {
            await admin.PutAsJsonAsync(Settings, new { timeZone = "UTC" });
        }
    }

    /// <summary>
    /// Una fecha en formato español se lee como día/mes. Antes se probaba primero el formato
    /// invariante, que es mes/día, y «01/11/2026» era el 11 de enero.
    /// </summary>
    [Fact]
    public async Task A_spanish_date_is_day_then_month()
    {
        var admin = await LoginAsync("admin@acme.com", "admin123");
        var agentEmail = $"agente.{Guid.NewGuid():N}@acme.com";
        var agent = await admin.PostAsJsonAsync("/api/v1/users", new { Name = "Agente", Email = agentEmail, Password = MemberPassword, Role = "Member" });
        agent.EnsureSuccessStatusCode();
        var agentId = (await agent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var ticket = await admin.PostAsJsonAsync("/api/v1/tickets", new { Title = "Ticket de este mes", Description = "", Priority = "Low" });
        var ticketId = (await ticket.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await admin.PatchAsJsonAsync($"/api/v1/tickets/{ticketId}", new { AssignedAgentId = agentId })).EnsureSuccessStatusCode();

        // El día 1 del mes que viene, escrito a la española: el día va primero y es 01, así que
        // leído como mes/día sería una fecha de enero, anterior a hoy salvo en los primeros días.
        var today = DateTime.UtcNow;
        var nextMonth = new DateTime(today.Year, today.Month, 1).AddMonths(1);

        var response = await admin.PostAsJsonAsync("/api/v1/reports/preview", new
        {
            definition = new
            {
                dataSource = "Tickets", groupBy = "status", measure = "count", visualization = "table",
                filters = new object[]
                {
                    new { field = "agent", @operator = "is", value = agentId.ToString() },
                    new { field = "created_at", @operator = "less_than", value = nextMonth.ToString("dd/MM/yyyy") },
                },
            },
            title = "Tickets de este mes",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var rows = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rows").EnumerateArray().ToList();
        rows.Sum(r => int.Parse(r[1].GetString()!)).Should().Be(1, $"«{nextMonth:dd/MM/yyyy}» es el 1 del mes que viene");
    }
}
