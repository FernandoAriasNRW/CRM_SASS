using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Webhook.Application.Abstractions;
using Webhook.Application.Abstractions.Repositories;
using Webhook.Application.DTOs;
using Webhook.Domain.Entities;

namespace Webhook.Infrastructure.Services;

public sealed class WebhookDispatchService(
    IWebhookSubscriptionRepository repository,
    IHttpClientFactory httpClientFactory) : IWebhookDispatchService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task DispatchAsync(string eventName, Guid tenantId, object eventData, CancellationToken ct = default)
    {
        var subscriptions = await repository.GetActiveByEventAsync(eventName, ct);
        var active = subscriptions.Where(s => s.TenantId == tenantId).ToList();
        if (active.Count == 0) return;

        var payload = new WebhookPayload(eventName, tenantId, DateTime.UtcNow, eventData);
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        var client = httpClientFactory.CreateClient("webhook");
        await Task.WhenAll(active.Select(s => DeliverAsync(client, s, json, ct)));
    }

    private static async Task DeliverAsync(HttpClient client, WebhookSubscription sub, string json, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, sub.TargetUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Webhook-Event", sub.EventName);
        request.Headers.Add("X-Webhook-Signature", Sign(json, sub.Secret));

        try { await client.SendAsync(request, ct); }
        catch { /* delivery failures are fire-and-forget */ }
    }

    private static string Sign(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
