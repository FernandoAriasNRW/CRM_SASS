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
}
