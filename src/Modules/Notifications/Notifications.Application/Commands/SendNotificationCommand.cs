using BuildingBlocks.Application.Abstractions;

namespace Notifications.Application.Commands;

public sealed record SendNotificationCommand(
    Guid TenantId,
    Guid NotificationId
) : ICommand<bool>;
