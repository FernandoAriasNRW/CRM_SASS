using Webhook.Domain.Entities;

namespace Webhook.Application.Abstractions.Repositories;

public interface IWebhookSubscriptionRepository
{
    Task<WebhookSubscription?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<WebhookSubscription>> GetByTenantAsync(Guid tenantId, string? eventName = null, CancellationToken ct = default);

    Task<IReadOnlyList<WebhookSubscription>> GetActiveByEventAsync(string eventName, CancellationToken ct = default);

    Task AddAsync(WebhookSubscription subscription, CancellationToken ct = default);

    Task UpdateAsync(WebhookSubscription subscription, CancellationToken ct = default);

    Task DeleteAsync(WebhookSubscription subscription, CancellationToken ct = default);
}
