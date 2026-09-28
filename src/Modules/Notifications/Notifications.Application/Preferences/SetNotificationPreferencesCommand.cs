using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Notifications.Domain.Entities;

namespace Notifications.Application.Preferences;

public sealed record SetNotificationPreferencesCommand(
    Guid TenantId,
    Guid UserId,
    bool EmailEnabled,
    bool PushEnabled,
    bool TaskAssigned,
    bool TaskCompleted,
    bool TaskDueSoon,
    bool TicketCreated,
    bool TicketUpdated,
    bool ProjectUpdated,
    bool MentionEnabled,
    bool ExportReady,
    bool QuietHoursEnabled,
    string QuietHoursStart,
    string QuietHoursEnd) : ICommand<NotificationPreferencesDto>;
