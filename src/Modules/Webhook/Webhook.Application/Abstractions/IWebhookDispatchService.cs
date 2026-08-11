namespace Webhook.Application.Abstractions;

public interface IWebhookDispatchService
{
    Task DispatchAsync(string eventName, Guid tenantId, object eventData, CancellationToken ct = default);
}
