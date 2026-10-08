using Microsoft.EntityFrameworkCore;
using Webhook.Domain.Entities;
using Webhook.Infrastructure.Persistence;

namespace ApiHost.Seeding;

public sealed class WebhookSeeder(TimeProvider timeProvider, WebhookDbContext webhookDb) : IModuleSeeder
{
    public string Module => "Webhook";
    public int Order => 100;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;

        using var _ = webhookDb.AsTenant(tenantId);
        try { await OrphanRows.AdoptAsync(webhookDb, "WebhookSubscriptions", "TenantId", tenantId, cancellationToken); } catch { }

        if (await webhookDb.Subscriptions.AnyAsync(w => w.TenantId == tenantId, cancellationToken))
            return;

        // Ejemplos para enseñar la pantalla, y por eso desactivados y contra example.com. Apuntaban
        // a Slack, Zapier y Datadog de verdad: con la entrega en segundo plano, una base de
        // demostración habría empezado a mandar sus datos a servicios ajenos.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var examples = new[]
        {
            WebhookSubscription.Create(now, tenantId, "Avisos de tareas (ejemplo)", "https://example.com/webhooks/tasks", ["task.created", "task.status_changed"], "whsec_ejemplo_tareas"),
            WebhookSubscription.Create(now, tenantId, "Tickets al CRM (ejemplo)", "https://example.com/webhooks/tickets", ["ticket.created", "ticket.updated"], "whsec_ejemplo_tickets"),
            WebhookSubscription.Create(now, tenantId, "Altas de usuarios (ejemplo)", "https://example.com/webhooks/users", ["user.created"], "whsec_ejemplo_usuarios"),
        };
        foreach (var example in examples)
            example.Update(now, example.Name, example.TargetUrl, example.EventTypes, isActive: false);

        webhookDb.Subscriptions.AddRange(examples);
        await webhookDb.SaveChangesAsync(cancellationToken);
    }
}
