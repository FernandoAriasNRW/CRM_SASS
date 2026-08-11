using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Abstractions.Queries;
using Notifications.Application.DTOs;
using Notifications.Domain.ValueObjects;
using Notifications.Infrastructure.Persistence;

namespace Notifications.Infrastructure.Queries;

public sealed class NotificationQueries(NotificationsDbContext context) : INotificationQueries
{
    public async Task<PagedResult<NotificationDto>> GetByTenantAsync(
        Guid tenantId, Guid? recipientId, string? type, string? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = context.Notifications.AsNoTracking().Where(n => n.TenantId == tenantId);

        if (recipientId.HasValue) query = query.Where(n => n.RecipientUserId == recipientId.Value);
        if (!string.IsNullOrEmpty(type)) query = query.Where(n => n.TypeValue == type);
        if (!string.IsNullOrEmpty(status)) query = query.Where(n => n.StatusValue == status);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.TenantId, n.RecipientUserId, n.SenderUserId,
                n.TypeValue, n.StatusValue, n.Subject, n.Body, n.Metadata,
                n.CreatedAt, n.SentAt, n.ReadAt))
            .ToListAsync(ct);

        return PagedResult<NotificationDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<NotificationDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await context.Notifications.AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.Id == id)
            .Select(n => new NotificationDto(n.Id, n.TenantId, n.RecipientUserId, n.SenderUserId,
                n.TypeValue, n.StatusValue, n.Subject, n.Body, n.Metadata,
                n.CreatedAt, n.SentAt, n.ReadAt))
            .FirstOrDefaultAsync(ct);

    public async Task<int> GetUnreadCountAsync(Guid tenantId, Guid recipientId, CancellationToken ct = default)
        => await context.Notifications.CountAsync(
            n => n.TenantId == tenantId && n.RecipientUserId == recipientId
              && n.StatusValue == NotificationStatus.Pending.Name && !n.IsDeleted, ct);
}
