using BuildingBlocks.Application.Abstractions;

namespace Notifications.Application.Commands;

public sealed record MarkNotificationAsReadCommand(
    Guid TenantId,
    Guid NotificationId,
    Guid RecipientUserId
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "notification.read";
}
