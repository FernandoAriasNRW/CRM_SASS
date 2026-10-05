using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Las preferencias de aviso, de punta a punta.
///
/// Existen por un defecto que la pantalla escondía perfectamente: `GET /preferences` devolvía un
/// objeto con valores fijos escritos en el propio endpoint y `PUT` respondía con el mismo cuerpo
/// que recibía, **sin guardar nada**. Los interruptores se movían, salía el aviso de
/// «guardado»… y al recargar todo volvía a su sitio.
///
/// La prueba que lo destapa no puede quedarse en el código de estado ni en lo que devuelve el
/// `PUT`: hay que **volver a preguntar** en otra petición. Es la misma lección del `PATCH` que
/// no guardaba, en la Fase 4: una prueba que sólo mira el código de estado no ve un guardado
/// que no guarda.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class NotificationPreferencesFlowTests(CrmApiFactory factory)
{
    private const string Email = "admin@acme.com";
    private const string Password = "admin123";
    private const string Route = "/api/v1/notifications/preferences";

    private async Task<HttpClient> AuthenticateAsync()
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email, Password });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    /// <summary>Un cuerpo completo, para no depender de los valores por omisión al enviar.</summary>
    private static object Body(
        bool emailEnabled = true, bool taskCompleted = false, bool exportReady = true,
        bool quietHoursEnabled = false, string start = "22:00", string end = "08:00") => new
        {
            emailEnabled,
            pushEnabled = false,
            taskAssigned = true,
            taskCompleted,
            taskDueSoon = true,
            ticketCreated = true,
            ticketUpdated = false,
            projectUpdated = true,
            mentionEnabled = true,
            exportReady,
            quietHoursEnabled,
            quietHoursStart = start,
            quietHoursEnd = end,
        };

    [Fact]
    public async Task Untouched_preferences_return_the_defaults()
    {
        var client = await AuthenticateAsync();

        var preferences = await client.GetFromJsonAsync<JsonElement>(Route);

        preferences.GetProperty("taskAssigned").GetBoolean().Should().BeTrue("un aviso que la persona espera llega encendido");
        preferences.GetProperty("exportReady").GetBoolean().Should().BeTrue("era una condición del encargo");
        preferences.GetProperty("quietHoursStart").GetString().Should().Be("22:00");
    }

    /// <summary>
    /// La prueba que importa: se guarda, se vuelve a preguntar **en otra petición**, y tiene
    /// que salir lo guardado. Con el endpoint anterior esto fallaba.
    /// </summary>
    [Fact]
    public async Task What_is_saved_is_still_there_on_reread()
    {
        var client = await AuthenticateAsync();

        var saved = await client.PutAsJsonAsync(Route, Body(emailEnabled: false, taskCompleted: true));
        saved.StatusCode.Should().Be(HttpStatusCode.OK);

        var reread = await client.GetFromJsonAsync<JsonElement>(Route);

        reread.GetProperty("emailEnabled").GetBoolean().Should().BeFalse();
        reread.GetProperty("taskCompleted").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// El aviso de exportación se puede apagar. Es lo que se pidió explícitamente: llega
    /// encendido, pero la persona manda.
    /// </summary>
    [Fact]
    public async Task The_export_notification_can_be_turned_off_and_stays_off()
    {
        var client = await AuthenticateAsync();

        await client.PutAsJsonAsync(Route, Body(exportReady: false));

        var reread = await client.GetFromJsonAsync<JsonElement>(Route);
        reread.GetProperty("exportReady").GetBoolean().Should().BeFalse();

        // Y se puede volver a encender: una preferencia que sólo se puede apagar es una trampa.
        await client.PutAsJsonAsync(Route, Body(exportReady: true));
        (await client.GetFromJsonAsync<JsonElement>(Route)).GetProperty("exportReady").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Quiet_hours_are_saved()
    {
        var client = await AuthenticateAsync();

        await client.PutAsJsonAsync(Route, Body(quietHoursEnabled: true, start: "23:30", end: "07:15"));

        var reread = await client.GetFromJsonAsync<JsonElement>(Route);

        reread.GetProperty("quietHoursEnabled").GetBoolean().Should().BeTrue();
        reread.GetProperty("quietHoursStart").GetString().Should().Be("23:30");
        reread.GetProperty("quietHoursEnd").GetString().Should().Be("07:15");
    }

    /// <summary>
    /// Una hora ilegible se rechaza en vez de sustituirse por algo razonable. Guardar en
    /// silencio unas horas distintas de las que la persona escribió es de los fallos que se
    /// descubren semanas después, al no recibir un aviso.
    /// </summary>
    [Fact]
    public async Task An_unreadable_time_returns_an_error_instead_of_guessing()
    {
        var client = await AuthenticateAsync();

        var response = await client.PutAsJsonAsync(Route, Body(quietHoursEnabled: true, start: "las diez"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Un tramo de silencio de longitud cero no silencia nada: sólo puede ser un error.</summary>
    [Fact]
    public async Task Quiet_hours_starting_and_ending_at_the_same_time_are_rejected()
    {
        var client = await AuthenticateAsync();

        var response = await client.PutAsJsonAsync(
            Route, Body(quietHoursEnabled: true, start: "22:00", end: "22:00"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Without_authentication_they_are_neither_seen_nor_changed()
    {
        var anonymous = factory.CreateClient();

        (await anonymous.GetAsync(Route)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PutAsJsonAsync(Route, Body())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
