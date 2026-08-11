using BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;

namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Application;

public interface IWebhookRepository
{
    Task<List<WebhookEntity>> GetByEventAsync(string eventName, CancellationToken ct);
}