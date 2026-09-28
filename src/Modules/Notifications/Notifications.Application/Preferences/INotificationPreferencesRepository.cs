using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Notifications.Domain.Entities;

namespace Notifications.Application.Preferences;

public interface INotificationPreferencesRepository
{
    Task<NotificationPreferences?> GetForUserAsync(Guid tenantId, Guid userId, CancellationToken ct);
    Task AddAsync(NotificationPreferences preferences, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
