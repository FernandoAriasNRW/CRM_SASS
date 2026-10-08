using Microsoft.EntityFrameworkCore;
using Webhook.Application.Abstractions.Repositories;
using Webhook.Domain.Entities;
using Webhook.Infrastructure.Persistence;

namespace Webhook.Infrastructure.Repositories;

public sealed class EfWebhookSubscriptionRepository(WebhookDbContext context) : IWebhookSubscriptionRepository
{
    public Task<WebhookSubscription?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => context.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);

    public async Task<IReadOnlyList<WebhookSubscription>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await context.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

    /// <summary>
    /// Se traen las activas de la organización y se filtra el evento en memoria: la lista va en una
    /// columna JSON y una organización tiene unas pocas suscripciones, no miles.
    /// </summary>
    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveForEventAsync(Guid tenantId, string eventName, CancellationToken ct = default)
    {
        var active = await context.Subscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .ToListAsync(ct);

        return active.Where(s => s.Subscribes(eventName)).ToList();
    }

    public async Task AddAsync(WebhookSubscription subscription, CancellationToken ct = default)
        => await context.Subscriptions.AddAsync(subscription, ct);

    public void Remove(WebhookSubscription subscription) => context.Subscriptions.Remove(subscription);
}

public sealed class EfWebhookDeliveryRepository(WebhookDbContext context) : IWebhookDeliveryRepository
{
    public async Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default)
        => await context.Deliveries.AddAsync(delivery, ct);

    public async Task<IReadOnlyList<WebhookDelivery>> GetRecentAsync(Guid tenantId, Guid subscriptionId, int max, CancellationToken ct = default)
        => await context.Deliveries.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.SubscriptionId == subscriptionId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(max)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, WebhookDeliveryStats>> GetStatsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var rows = await context.Deliveries.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .GroupBy(d => d.SubscriptionId)
            .Select(g => new
            {
                SubscriptionId = g.Key,
                Succeeded = g.Count(d => d.Status == WebhookDeliveryStatus.Succeeded),
                Failed = g.Count(d => d.Status == WebhookDeliveryStatus.Failed),
                Pending = g.Count(d => d.Status == WebhookDeliveryStatus.Pending),
                Last = g.Max(d => (DateTime?)d.CompletedAtUtc),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.SubscriptionId, r => new WebhookDeliveryStats(r.Succeeded, r.Failed, r.Pending, r.Last));
    }
}
