using BuildingBlocks.Application.Abstractions;

namespace Identity.Application.Commands;

public sealed record DeleteUserCommand(
    Guid TenantId,
    Guid UserId,
    Guid DeletedBy
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "identity.user.deleted";
}
