using BuildingBlocks.Application.Abstractions;

namespace Notifications.Application.Commands;

public sealed record DeleteNotificationCommand(
    Guid TenantId,
    Guid NotificationId,
    Guid DeletedBy
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "notification.deleted";
}
