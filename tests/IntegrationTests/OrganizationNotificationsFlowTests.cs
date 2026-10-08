using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Webhook.Domain.Entities;
using Webhook.Infrastructure.Persistence;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Los avisos del chat, de los equipos, de las cuentas y de los webhooks.
///
/// Los de cuentas y webhooks son de administración: quien no administra no los ve en sus
/// preferencias, no los puede encender y no los recibe.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OrganizationNotificationsFlowTests(CrmApiFactory factory)
{
    private const string MemberPassword = "Miembro2026!x";
    private const string Preferences = "/api/v1/notifications/preferences";

    private async Task<HttpClient> LoginAsync(string email, string password)
    {
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private Task<HttpClient> AdminAsync() => LoginAsync("admin@acme.com", "admin123");

    private async Task<(HttpClient Client, Guid Id, string Name)> MemberAsync(HttpClient admin, string name, string role = "Member")
    {
        var email = $"{name.ToLowerInvariant()}.{Guid.NewGuid():N}@acme.com";
        var created = await admin.PostAsJsonAsync("/api/v1/users", new { Name = name, Email = email, Password = MemberPassword, Role = role });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        return (await LoginAsync(email, MemberPassword), id, name);
    }

    private static async Task<List<JsonElement>> NotificationsAsync(HttpClient client, string kind)
        => (await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications?pageSize=200"))
            .GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("kind").GetString() == kind)
            .ToList();

    private static Task<HttpResponseMessage> SetTypeAsync(HttpClient client, string kind, bool enabled)
        => client.PutAsJsonAsync(Preferences, new
        {
            emailEnabled = true, pushEnabled = false, quietHoursEnabled = false, quietHoursStart = "22:00", quietHoursEnd = "08:00",
            types = new[] { new { kind, enabled } },
        });

    /// <summary>Un mensaje avisa a quien ya ha escrito en la conversación, y no a quien lo manda.</summary>
    [Fact]
    public async Task A_message_reaches_whoever_has_written_in_the_conversation()
    {
        var admin = await AdminAsync();
        var (ana, _, _) = await MemberAsync(admin, "Ana");
        var (luis, _, _) = await MemberAsync(admin, "Luis");

        var channel = await admin.PostAsJsonAsync("/api/v1/channels", new { name = "#avisos-" + Guid.NewGuid().ToString("N")[..6], type = "Channel" });
        channel.EnsureSuccessStatusCode();
        var channelId = (await channel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await ana.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", new { content = "Hola, ¿alguien?" })).EnsureSuccessStatusCode();
        (await luis.PostAsJsonAsync($"/api/v1/channels/{channelId}/messages", new { content = "Aquí estoy" })).EnsureSuccessStatusCode();

        var forAna = (await NotificationsAsync(ana, "chat.message"))
            .Where(n => n.GetProperty("entityId").GetGuid() == channelId).ToList();
        forAna.Should().ContainSingle().Which.GetProperty("body").GetString().Should().Be("Aquí estoy");
        forAna[0].GetProperty("entityType").GetString().Should().Be("Conversation");

        (await NotificationsAsync(luis, "chat.message"))
            .Should().NotContain(n => n.GetProperty("entityId").GetGuid() == channelId, "lo ha escrito él");
    }

    [Fact]
    public async Task Whoever_joins_or_leaves_a_team_is_told()
    {
        var admin = await AdminAsync();
        var (ana, anaId, _) = await MemberAsync(admin, "Ana");
        var name = "Equipo de avisos " + Guid.NewGuid().ToString("N")[..6];

        var created = await admin.PostAsJsonAsync("/api/v1/teams", new { name, description = "", memberIds = new[] { anaId } });
        created.EnsureSuccessStatusCode();
        var teamId = await created.Content.ReadFromJsonAsync<Guid>();

        (await NotificationsAsync(ana, "team.member_added"))
            .Should().ContainSingle(n => n.GetProperty("entityId").GetGuid() == teamId);

        (await admin.PutAsJsonAsync($"/api/v1/teams/{teamId}", new { name, description = "", memberIds = Array.Empty<Guid>() }))
            .EnsureSuccessStatusCode();

        (await NotificationsAsync(ana, "team.member_removed"))
            .Should().ContainSingle(n => n.GetProperty("body").GetString()!.Contains(name));
    }

    /// <summary>
    /// Los avisos de cuentas son de administración: quien administra los puede encender y le llegan;
    /// quien no, ni los ve ni los puede encender.
    /// </summary>
    [Fact]
    public async Task Account_notifications_are_for_admins_only()
    {
        var admin = await AdminAsync();
        var (member, _, _) = await MemberAsync(admin, "Miembro");

        var memberPreferences = await member.GetFromJsonAsync<JsonElement>(Preferences);
        memberPreferences.GetProperty("types").EnumerateArray().Select(t => t.GetProperty("kind").GetString())
            .Should().NotContain("user.created", "quien no administra no ve los avisos de administración");
        (await SetTypeAsync(member, "user.created", true)).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "ni los puede encender por la API");

        // Otra administradora, que es la que se entera: quien crea la cuenta no recibe aviso de lo
        // que acaba de hacer.
        var (otherAdmin, _, _) = await MemberAsync(admin, "Administradora", role: "Admin");
        (await SetTypeAsync(otherAdmin, "user.created", true)).EnsureSuccessStatusCode();

        var (_, _, name) = await MemberAsync(admin, "Recienllegada");

        (await NotificationsAsync(otherAdmin, "user.created"))
            .Should().Contain(n => n.GetProperty("body").GetString()!.Contains(name));
        (await NotificationsAsync(member, "user.created")).Should().BeEmpty("no administra");
    }

    /// <summary>
    /// Un webhook que agota sus reintentos avisa a quien administra. Esperar seis intentos con sus
    /// pausas llevaría horas, así que la prueba deja el envío en su último intento y espera a que el
    /// trabajo de entrega lo dé por perdido.
    /// </summary>
    [Fact]
    public async Task A_webhook_that_gives_up_tells_the_admins()
    {
        var admin = await AdminAsync();
        var url = $"https://hooks.example.test/{Guid.NewGuid():N}/fail";
        var subscription = await admin.PostAsJsonAsync("/api/v1/webhooks", new { name = "Destino caído", url, eventTypes = new[] { "team.created" } });
        subscription.EnsureSuccessStatusCode();
        var subscriptionId = (await subscription.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("subscription").GetProperty("id").GetGuid();

        (await admin.PostAsJsonAsync("/api/v1/teams", new { name = "Equipo que dispara", description = "", memberIds = Array.Empty<Guid>() })).EnsureSuccessStatusCode();
        await factory.Webhooks.WaitForAsync(r => r.Url.ToString() == url);

        // El envío al borde: un intento más y se agota.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
            var attemptsLeft = WebhookDelivery.MaxAttempts - 1;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE WebhookDeliveries SET Attempts = {attemptsLeft}, NextAttemptAtUtc = UTC_TIMESTAMP(6) WHERE SubscriptionId = {subscriptionId}");
        }

        for (var i = 0; i < 40 && (await NotificationsAsync(admin, "webhook.delivery_failed"))
                 .All(n => !n.GetProperty("body").GetString()!.Contains("Destino caído")); i++)
            await Task.Delay(250);

        (await NotificationsAsync(admin, "webhook.delivery_failed"))
            .Should().Contain(n => n.GetProperty("body").GetString()!.Contains("Destino caído"));
    }
}
