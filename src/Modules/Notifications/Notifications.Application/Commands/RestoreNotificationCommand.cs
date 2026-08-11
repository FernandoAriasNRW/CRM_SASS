using BuildingBlocks.Application.Abstractions;

namespace Notifications.Application.Commands;

public sealed record RestoreNotificationCommand(
    Guid TenantId,
    Guid NotificationId
) : ICommand<bool>;
