using BuildingBlocks.Application.Abstractions;
using Notifications.Domain.Entities;

namespace Notifications.Application.Commands;

public sealed record CreateNotificationCommand(
    Guid TenantId,
    Guid RecipientUserId,
    string Type,
    string Subject,
    string Body,
    Guid? SenderUserId = null
) : ICommand<Notification>, IWebhookTriggered
{
    public string WebhookEventName => "notification.created";
}
