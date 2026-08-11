using Microsoft.EntityFrameworkCore;
using Webhook.Application.Abstractions.Repositories;
using Webhook.Domain.Entities;
using Webhook.Infrastructure.Persistence;

namespace Webhook.Infrastructure.Repositories;

public sealed class EfWebhookSubscriptionRepository(WebhookDbContext context) : IWebhookSubscriptionRepository
{
    public async Task<WebhookSubscription?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await context.Subscriptions.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);

    public async Task<IReadOnlyList<WebhookSubscription>> GetByTenantAsync(
        Guid tenantId, string? eventName = null, CancellationToken ct = default)
    {
        var query = context.Subscriptions.AsNoTracking().Where(s => s.TenantId == tenantId);
        if (!string.IsNullOrEmpty(eventName))
            query = query.Where(s => s.EventName == eventName);
        return await query.OrderBy(s => s.EventName).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveByEventAsync(
        string eventName, CancellationToken ct = default)
        => await context.Subscriptions.AsNoTracking()
            .Where(s => s.EventName == eventName && s.IsActive)
            .ToListAsync(ct);

    public async Task AddAsync(WebhookSubscription subscription, CancellationToken ct = default)
        => await context.Subscriptions.AddAsync(subscription, ct);

    public Task UpdateAsync(WebhookSubscription subscription, CancellationToken ct = default)
    {
        context.Subscriptions.Update(subscription);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(WebhookSubscription subscription, CancellationToken ct = default)
    {
        context.Subscriptions.Remove(subscription);
        return Task.CompletedTask;
    }
}
