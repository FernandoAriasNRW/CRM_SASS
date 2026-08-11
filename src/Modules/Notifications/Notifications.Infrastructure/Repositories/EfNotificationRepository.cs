using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Abstractions.Repositories;
using Notifications.Domain.Entities;
using Notifications.Domain.ValueObjects;
using Notifications.Infrastructure.Persistence;

namespace Notifications.Infrastructure.Repositories;

public sealed class EfNotificationRepository(NotificationsDbContext context) : INotificationRepository
{
    public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetByTenantAsync(
        Guid tenantId, Guid? recipientId, string? type, string? status,
        PaginationRequest pagination, CancellationToken ct = default)
    {
        var query = context.Notifications.Where(n => n.TenantId == tenantId);

        if (recipientId.HasValue)
            query = query.Where(n => n.RecipientUserId == recipientId.Value);
        if (!string.IsNullOrEmpty(type))
            query = query.Where(n => n.TypeValue == type);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(n => n.StatusValue == status);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(pagination.Skip).Take(pagination.Take)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Notification?> GetByIdAsync(Guid tenantId, Guid id, bool includeDeleted = false, CancellationToken ct = default)
    {
        var query = context.Notifications.AsQueryable();
        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        return await query.AsNoTracking()
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id, ct);
    }

    public async Task<Notification> AddAsync(Notification notification, CancellationToken ct = default)
    {
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(ct);
        return notification;
    }

    public async Task<bool> UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        var entity = await context.Notifications
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.TenantId == notification.TenantId && n.Id == notification.Id, ct);

        if (entity is null) return false;

        if (notification.StatusValue == NotificationStatus.Read.Name && entity.StatusValue != NotificationStatus.Read.Name)
        {
            entity.MarkAsRead();
        }

        entity.UpdateContent(notification.Subject, notification.Body, notification.Metadata, notification.TypeValue);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> GetUnreadCountAsync(Guid tenantId, Guid recipientId, CancellationToken ct = default)
        => await context.Notifications.CountAsync(
            n => n.TenantId == tenantId && n.RecipientUserId == recipientId
              && n.StatusValue == NotificationStatus.Pending.Name && !n.IsDeleted, ct);

    public async Task DeleteAsync(Guid tenantId, Guid notificationId, Guid deletedBy, CancellationToken ct = default)
    {
        var entity = await context.Notifications.IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == notificationId, ct);

        if (entity is not null && !entity.IsDeleted)
        {
            entity.Delete(deletedBy);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task RestoreAsync(Guid tenantId, Guid notificationId, CancellationToken ct = default)
    {
        var entity = await context.Notifications.IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == notificationId, ct);

        if (entity is not null && entity.IsDeleted)
        {
            entity.Restore();
            await context.SaveChangesAsync(ct);
        }
    }
}
