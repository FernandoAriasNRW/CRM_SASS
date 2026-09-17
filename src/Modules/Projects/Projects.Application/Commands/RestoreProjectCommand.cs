using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Domain.Entities;

namespace Projects.Application.Commands;

public sealed record RestoreProjectCommand(
    Guid TenantId,
    Guid Id
) : ICommand<Project>, IWebhookTriggered, IAuthorizeEntity
{
    public string EntityType => "Project";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";

    public string WebhookEventName => "project.restored";
}

