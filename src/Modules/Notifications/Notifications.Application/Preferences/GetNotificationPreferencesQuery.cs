using BuildingBlocks.Application.Abstractions;

namespace Notifications.Application.Preferences;

public sealed record GetNotificationPreferencesQuery(Guid TenantId, Guid UserId, bool IsAdmin) : IQuery<NotificationPreferencesDto>;
