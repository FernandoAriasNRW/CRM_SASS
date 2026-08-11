using BuildingBlocks.Domain;
using Notifications.Domain.Entities;

namespace Notifications.Application.Abstractions.Repositories;

public interface INotificationRepository
{
  Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetByTenantAsync(
      Guid tenantId,
      Guid? recipientId,
      string? type,
      string? status,
      PaginationRequest pagination,
      CancellationToken ct = default);

  Task<Notification?> GetByIdAsync(Guid tenantId, Guid id, bool includeDeleted = false, CancellationToken ct = default);

  Task<Notification> AddAsync(Notification notification, CancellationToken ct = default);

  Task<bool> UpdateAsync(Notification notification, CancellationToken ct = default);

  Task<int> GetUnreadCountAsync(Guid tenantId, Guid recipientId, CancellationToken ct = default);

  // Soft delete operations
  Task DeleteAsync(Guid tenantId, Guid notificationId, Guid deletedBy, CancellationToken ct = default);

  Task RestoreAsync(Guid tenantId, Guid notificationId, CancellationToken ct = default);
}