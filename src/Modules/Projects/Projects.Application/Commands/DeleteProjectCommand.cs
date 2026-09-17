using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;

namespace Projects.Application.Commands;

public sealed record DeleteProjectCommand(
    Guid TenantId,
    Guid Id,
    Guid DeletedBy
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string EntityType => "Project";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";

    public string WebhookEventName => "project.deleted";
}