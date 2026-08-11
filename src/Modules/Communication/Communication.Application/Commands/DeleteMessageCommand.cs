using BuildingBlocks.Application.Abstractions;

namespace Communication.Application.Commands;

public sealed record DeleteMessageCommand(
    Guid TenantId,
    Guid MessageId,
    Guid DeletedBy
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "communication.message.deleted";
}
