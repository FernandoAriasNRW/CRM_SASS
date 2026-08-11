using BuildingBlocks.Application.Abstractions;

namespace Communication.Application.Commands;

public sealed record DeleteConversationCommand(
    Guid TenantId,
    Guid ConversationId,
    Guid DeletedBy
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "communication.conversation.deleted";
}
