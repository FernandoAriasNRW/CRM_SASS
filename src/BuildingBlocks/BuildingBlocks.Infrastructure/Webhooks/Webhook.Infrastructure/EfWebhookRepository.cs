using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Webhooks.Webhook.Application;
using BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;

namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Infrastructure;

public class EfWebhookRepository : IWebhookRepository
{
    private readonly CrmDbContext _db;

    public EfWebhookRepository(CrmDbContext db)
    {
        _db = db;
    }

    public async Task<List<WebhookEntity>> GetByEventAsync(string eventName, CancellationToken ct)
    {
        return await _db.Set<WebhookEntity>()
            .Where(x => x.Event == eventName && x.IsActive)
            .ToListAsync(ct);
    }
}