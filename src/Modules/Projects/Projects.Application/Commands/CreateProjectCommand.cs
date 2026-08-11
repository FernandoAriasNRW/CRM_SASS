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
) : ICommand<Project>, IWebhookTriggered
{
    public string WebhookEventName => "project.created";
}
