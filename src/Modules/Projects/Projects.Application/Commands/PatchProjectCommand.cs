using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;

namespace Projects.Application.Commands;

public sealed record PatchProjectCommand(
    Guid TenantId,
    Guid Id,
    string? Name,
    string? Description,
    string? Status,
    DateOnly? EstimatedEndDate
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string EntityType => "Project";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";

    public string WebhookEventName => "project.updated";
}
