using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;

namespace WorkItems.Application.Commands;

/// <summary>
/// Registra que <paramref name="Id"/> queda bloqueada por <paramref name="DependsOnTaskId"/>.
/// </summary>
public sealed record AddTaskDependencyCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole,
    Guid DependsOnTaskId
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.dependency.added";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}

public sealed record RemoveTaskDependencyCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole,
    Guid DependsOnTaskId
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.dependency.removed";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}
