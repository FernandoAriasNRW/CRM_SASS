using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Los informes programados, contra la API levantada.
///
/// Lo que <b>no</b> se comprueba aquí es que un informe salga solo el lunes a las 8: eso exigiría
/// esperar a un lunes. La decisión de cuándo toca es una función pura del dominio
/// —<c>ReportSchedule.IsDue</c>— y está probada al detalle en las unitarias, con sus
/// casos límite: el domingo, el día 1, el minuto antes de la hora, y el que impide que un informe
/// diario llegue doce veces.
///
/// Aquí se comprueba el contrato: que se pueda programar, ver, apagar y quitar, y que lo que no
/// tiene sentido se rechace diciendo por qué.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReportScheduleFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";

    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<Guid> CreateReportAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/reports", new
        {
            Name = $"Programado {Guid.NewGuid():N}"[..30],
            Type = "KpiSummary",
            Format = "Pdf"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task A_report_can_be_scheduled_and_read_back()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/reports/{reportId}/schedules", new
        {
            Frequency = "Weekly",
            Format = "Pdf",
            Time = "08:00",
            Day = 1
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        // Se relee en otra petición, no se mira lo que devolvió el POST: es la lección del PATCH
        // que respondía 200 sin guardar.
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{reportId}/schedules");

        var own = list.EnumerateArray().Should().ContainSingle().Subject;
        own.GetProperty("frequency").GetString().Should().Be("Weekly");
        own.GetProperty("time").GetString().Should().Be("08:00");
        own.GetProperty("day").GetInt32().Should().Be(1);
        own.GetProperty("isActive").GetBoolean().Should().BeTrue();
        own.GetProperty("lastGeneratedDay").ValueKind.Should().Be(JsonValueKind.Null,
            "recién creada no se ha generado nunca");
    }

    [Fact]
    public async Task A_new_report_has_no_schedules()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{reportId}/schedules");

        list.EnumerateArray().Should().BeEmpty();
    }

    /// <summary>
    /// Apagar y encender, sin borrar.
    ///
    /// Es la diferencia que importa: apagar conserva la marca del último día generado, así que
    /// volver a encender el mismo día no dispara el informe por segunda vez. Borrar y recrear sí
    /// lo haría.
    /// </summary>
    [Fact]
    public async Task A_schedule_can_be_turned_off_and_on_again()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var created = await client.PostAsJsonAsync($"/api/v1/reports/{reportId}/schedules",
            new { Frequency = "Daily", Format = "Csv", Time = "07:30", Day = (int?)null });

        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await client.PatchAsJsonAsync($"/api/v1/schedules/{id}", new { IsActive = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var off = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{reportId}/schedules");
        off.EnumerateArray().Single().GetProperty("isActive").GetBoolean().Should().BeFalse();

        (await client.PatchAsJsonAsync($"/api/v1/schedules/{id}", new { IsActive = true }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var on = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{reportId}/schedules");
        on.EnumerateArray().Single().GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task A_schedule_can_be_removed()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var created = await client.PostAsJsonAsync($"/api/v1/reports/{reportId}/schedules",
            new { Frequency = "Monthly", Format = "Excel", Time = "09:00", Day = 1 });

        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await client.DeleteAsync($"/api/v1/schedules/{id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{reportId}/schedules");
        list.EnumerateArray().Should().BeEmpty();
    }

    /// <summary>Quitar algo que ya no está no es un error: es el estado que se pedía.</summary>
    [Fact]
    public async Task Removing_a_missing_one_is_not_an_error()
    {
        var client = await AuthenticateAsync();

        (await client.DeleteAsync($"/api/v1/schedules/{Guid.NewGuid()}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    #region Lo que se rechaza, y por qué

    [Fact]
    public async Task An_unknown_frequency_lists_the_available_ones()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/reports/{reportId}/schedules",
            new { Frequency = "CadaHora", Format = "Pdf", Time = "08:00", Day = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var message = await response.Content.ReadAsStringAsync();
        message.Should().Contain("CadaHora").And.Contain("Daily");
    }

    [Fact]
    public async Task A_weekly_without_day_is_rejected()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/reports/{reportId}/schedules",
            new { Frequency = "Weekly", Format = "Pdf", Time = "08:00", Day = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("qué día");
    }

    /// <summary>
    /// El día 31 se rechaza, y el mensaje explica el porqué.
    ///
    /// Un informe mensual el día 31 no se generaría en febrero ni en los meses de treinta días:
    /// cuatro meses al año fallando en silencio. Es mejor no admitirlo que admitirlo y que falle.
    /// </summary>
    [Fact]
    public async Task Day_31_is_rejected_with_a_reason()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/reports/{reportId}/schedules",
            new { Frequency = "Monthly", Format = "Pdf", Time = "08:00", Day = 31 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("febrero");
    }

    [Fact]
    public async Task A_time_that_is_not_a_time_is_rejected()
    {
        var client = await AuthenticateAsync();
        var reportId = await CreateReportAsync(client);

        var response = await client.PostAsJsonAsync($"/api/v1/reports/{reportId}/schedules",
            new { Frequency = "Daily", Format = "Pdf", Time = "por la mañana", Day = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("08:00", "el mensaje enseña el formato");
    }

    [Fact]
    public async Task Scheduling_a_missing_report_is_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/reports/{Guid.NewGuid()}/schedules",
            new { Frequency = "Daily", Format = "Pdf", Time = "08:00", Day = (int?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Without_authentication_nothing_is_scheduled()
    {
        var anonymous = factory.CreateClient();

        (await anonymous.PostAsJsonAsync($"/api/v1/reports/{Guid.NewGuid()}/schedules",
                new { Frequency = "Daily", Format = "Pdf", Time = "08:00", Day = (int?)null }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
