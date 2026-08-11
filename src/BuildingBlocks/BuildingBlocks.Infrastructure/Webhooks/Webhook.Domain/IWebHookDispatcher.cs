namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;

public interface IWebhookDispatcher
{
  // Este es el método que usas en tu worker
  Task ProcessDeliveriesAsync(int batchSize, CancellationToken ct);
}