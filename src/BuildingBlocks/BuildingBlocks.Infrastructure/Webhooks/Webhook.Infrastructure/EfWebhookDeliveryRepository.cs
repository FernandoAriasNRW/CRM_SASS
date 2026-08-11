using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Webhooks.Webhook.Application;
using BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;

namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Infrastructure;

public class EfWebhookDeliveryRepository : IWebhookDeliveryRepository
{
  private readonly CrmDbContext _db;

  public EfWebhookDeliveryRepository(CrmDbContext db)
  {
    _db = db;
  }

  public async Task AddAsync(WebhookDelivery delivery, CancellationToken ct)
  {
    await _db.AddAsync(delivery, ct);
    await _db.SaveChangesAsync(ct);
  }

  public async Task UpdateAsync(WebhookDelivery delivery, CancellationToken ct)
  {
    _db.Update(delivery);
    await _db.SaveChangesAsync(ct);
  }
}