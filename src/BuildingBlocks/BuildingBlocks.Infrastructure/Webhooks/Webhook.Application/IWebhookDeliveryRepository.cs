using BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;

namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Application;

public interface IWebhookDeliveryRepository
{
  Task AddAsync(WebhookDelivery delivery, CancellationToken ct);

  Task UpdateAsync(WebhookDelivery delivery, CancellationToken ct);
}