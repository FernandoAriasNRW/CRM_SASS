using BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;

namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Application;

public interface IWebhookDeliveryService
{
    Task SendAsync(WebhookEntity webhook, string payload, CancellationToken ct);
}