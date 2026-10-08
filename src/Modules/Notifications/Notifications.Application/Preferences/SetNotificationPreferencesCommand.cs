using BuildingBlocks.Application.Abstractions;

namespace Notifications.Application.Preferences;

/// <summary>
/// Cambia las preferencias. Los tipos que no vienen en la lista se quedan como estaban: la
/// pantalla puede mandar sólo el que se tocó.
/// </summary>
public sealed record SetNotificationPreferencesCommand(
    Guid TenantId,
    Guid UserId,
    bool IsAdmin,
    bool EmailEnabled,
    bool PushEnabled,
    bool QuietHoursEnabled,
    string QuietHoursStart,
    string QuietHoursEnd,
    IReadOnlyList<NotificationTypeSetting> Types) : ICommand<NotificationPreferencesDto>;
