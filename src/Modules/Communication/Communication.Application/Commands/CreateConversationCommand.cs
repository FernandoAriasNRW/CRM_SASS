using BuildingBlocks.Application.Abstractions;
using Communication.Application.DTOs;

namespace Communication.Application.Commands;

public sealed record CreateConversationCommand(
    Guid TenantId,
    string Name,
    string Type
) : ICommand<ConversationDto>, IWebhookTriggered
{
    public string WebhookEventName => "communication.conversation.created";
}