using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Notifications.Domain.Entities;

namespace Notifications.Application.Preferences;

public interface INotificationPreferencesRepository
{
    Task<NotificationPreferences?> GetForUserAsync(Guid tenantId, Guid userId, CancellationToken ct);

    /// <summary>Las guardadas de varias personas a la vez. Quien no tiene fila no sale.</summary>
    Task<IReadOnlyDictionary<Guid, NotificationPreferences>> GetForUsersAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct);
    Task AddAsync(NotificationPreferences preferences, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
