using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;
using Projects.Domain.Entities;

namespace Projects.Application.Commands;
public sealed record CreateProjectCommand(
    Guid TenantId,
    Guid SpaceId,
    Guid? FolderId,
    Guid OwnerId,
    string Name,
    string Description,
    DateOnly EstimatedEndDate
) : ICommand<Project>, IWebhookTriggered, IAuthorizeEntity
{
    public string EntityType => "Project";
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";

    public string WebhookEventName => "project.created";
}
