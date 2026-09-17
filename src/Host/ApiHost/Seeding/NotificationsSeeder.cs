using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;
using Notifications.Domain.ValueObjects;
using Notifications.Infrastructure.Persistence;

namespace ApiHost.Seeding;

public sealed class NotificationsSeeder(NotificationsDbContext notificationsDb) : IModuleSeeder
{
    public string Module => "Notifications";
    public int Order => 90;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var adminId = context.Admin.Id;

        using var _ = notificationsDb.AsTenant(tenantId);
        try { await notificationsDb.Database.ExecuteSqlAsync($"UPDATE `Notifications` SET `TenantId` = {tenantId} WHERE `TenantId` != {tenantId}", cancellationToken); } catch { }

        if (await notificationsDb.Notifications.AnyAsync(n => n.TenantId == tenantId && n.RecipientUserId == adminId, cancellationToken))
            return;

        var assigned = Notification.Create(tenantId, adminId, NotificationType.InApp.Name, "Tarea Asignada", "Te han asignado la tarea: Implementar Webhooks v2");
        var priorityTicket = Notification.Create(tenantId, adminId, NotificationType.InApp.Name, "Nuevo Ticket Prioritario", "Se ha registrado un ticket sobre la consulta de permisos por rol.");
        var mention = Notification.Create(tenantId, adminId, NotificationType.InApp.Name, "Mención en Documento", "Sofia te ha mencionado en la especificación técnica de arquitectura.");

        if (assigned.IsSuccess && assigned.Value != null)
        {
            assigned.Value.MarkAsRead();
            notificationsDb.Notifications.Add(assigned.Value);
        }
        if (priorityTicket.IsSuccess && priorityTicket.Value != null) notificationsDb.Notifications.Add(priorityTicket.Value);
        if (mention.IsSuccess && mention.Value != null) notificationsDb.Notifications.Add(mention.Value);

        await notificationsDb.SaveChangesAsync(cancellationToken);
    }
}
