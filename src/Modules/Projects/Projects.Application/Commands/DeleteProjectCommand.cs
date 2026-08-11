using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;

namespace Projects.Application.Commands;

public sealed record DeleteProjectCommand(
    Guid TenantId,
    Guid Id,
    Guid DeletedBy
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "project.deleted";
}