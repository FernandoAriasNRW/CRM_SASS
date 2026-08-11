using BuildingBlocks.Domain;
using Notifications.Application.DTOs;

namespace Notifications.Application.Abstractions.Queries;

public interface INotificationQueries
{
    Task<PagedResult<NotificationDto>> GetByTenantAsync(
        Guid tenantId,
        Guid? recipientId,
        string? type,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<NotificationDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(Guid tenantId, Guid recipientId, CancellationToken ct = default);
}
