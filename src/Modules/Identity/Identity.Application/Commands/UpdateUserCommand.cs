using BuildingBlocks.Application.Abstractions;
using Identity.Application.DTOs;

namespace Identity.Application.Commands;

public sealed record UpdateUserCommand(
    Guid TenantId,
    Guid UserId,
    string? Name,
    string? Email,
    string? Role
) : ICommand<UserDto>, IWebhookTriggered
{
    public string WebhookEventName => "identity.user.updated";
}
