using BuildingBlocks.Application.Abstractions;
using Identity.Application.DTOs;

namespace Identity.Application.Commands;

public sealed record CreateUserCommand(
    Guid TenantId,
    string Name,
    string Email,
    string Password,
    string Role
) : ICommand<UserDto>, IWebhookTriggered
{
    public string WebhookEventName => "identity.user.registered";
}
