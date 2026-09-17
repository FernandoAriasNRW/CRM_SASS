using Microsoft.EntityFrameworkCore;
using Webhook.Domain.Entities;
using Webhook.Infrastructure.Persistence;

namespace ApiHost.Seeding;

public sealed class WebhookSeeder(WebhookDbContext webhookDb) : IModuleSeeder
{
    public string Module => "Webhook";
    public int Order => 100;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;

        using var _ = webhookDb.AsTenant(tenantId);
        try { await webhookDb.Database.ExecuteSqlAsync($"UPDATE `webhook_subscriptions` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

        if (await webhookDb.Subscriptions.AnyAsync(w => w.TenantId == tenantId, cancellationToken))
            return;

        webhookDb.Subscriptions.AddRange(
            WebhookSubscription.Create(tenantId, "task.created", "https://hooks.slack.com/services/T00/B00/X00", "whsec_slack_123456789"),
            WebhookSubscription.Create(tenantId, "ticket.updated", "https://hooks.zapier.com/hooks/catch/12345/abcde", "whsec_zapier_987654321"),
            WebhookSubscription.Create(tenantId, "user.created", "https://http-intake.logs.datadoghq.com/v1/input", "whsec_datadog_456789123"));
        await webhookDb.SaveChangesAsync(cancellationToken);
    }
}
