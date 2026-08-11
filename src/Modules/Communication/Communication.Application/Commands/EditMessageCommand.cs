using BuildingBlocks.Application.Abstractions;
using Communication.Application.DTOs;

namespace Communication.Application.Commands;

public sealed record EditMessageCommand(
    Guid TenantId,
    Guid MessageId,
    Guid SenderId,
    string NewContent
) : ICommand<MessageDto>, IWebhookTriggered
{
    public string WebhookEventName => "communication.message.edited";
}
