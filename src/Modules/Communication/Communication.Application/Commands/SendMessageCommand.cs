using BuildingBlocks.Application.Abstractions;
using Communication.Application.DTOs;

namespace Communication.Application.Commands;

public sealed record SendMessageCommand(
    Guid TenantId,
    Guid ConversationId,
    Guid SenderId,
    string Content
) : ICommand<MessageDto>, IWebhookTriggered
{
    public string WebhookEventName => "communication.message.sent";
}
