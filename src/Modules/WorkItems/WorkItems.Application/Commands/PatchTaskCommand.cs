using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using MediatR;

namespace WorkItems.Application.Commands;

public sealed record PatchTaskCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole,
    string? Title,
    string? Description,
    string? Status,
    string? Priority,
    Guid? AssigneeId,
    DateOnly? DueDate,
    decimal? EstimatedHours
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.patched";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}