using Webhook.Domain.Entities;

namespace Webhook.Application.Abstractions.Repositories;

public interface IWebhookSubscriptionRepository
{
    Task<WebhookSubscription?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<WebhookSubscription>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Las suscripciones activas de la organización que quieren este evento.</summary>
    Task<IReadOnlyList<WebhookSubscription>> GetActiveForEventAsync(Guid tenantId, string eventName, CancellationToken ct = default);

    Task AddAsync(WebhookSubscription subscription, CancellationToken ct = default);

    void Remove(WebhookSubscription subscription);
}

public interface IWebhookDeliveryRepository
{
    Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default);

    /// <summary>Los últimos envíos de una suscripción, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<WebhookDelivery>> GetRecentAsync(Guid tenantId, Guid subscriptionId, int max, CancellationToken ct = default);

    /// <summary>Cuántos envíos salieron bien, mal o siguen pendientes, por suscripción.</summary>
    Task<IReadOnlyDictionary<Guid, WebhookDeliveryStats>> GetStatsAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed record WebhookDeliveryStats(int Succeeded, int Failed, int Pending, DateTime? LastDeliveryAtUtc);
