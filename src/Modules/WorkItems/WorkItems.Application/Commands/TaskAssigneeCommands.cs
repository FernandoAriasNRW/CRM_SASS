using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;

namespace WorkItems.Application.Commands;

/// <summary>
/// Añade una persona responsable sin cambiar quién es la principal.
///
/// Cambiar la principal ya se hace con el patch de la tarea (<c>assigneeId</c>), que además la
/// mete en el conjunto: son dos intenciones distintas y conviene que la API las distinga.
/// </summary>
public sealed record AddTaskAssigneeCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole,
    Guid UserId
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.assignee.added";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}

public sealed record RemoveTaskAssigneeCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole,
    Guid UserId
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.assignee.removed";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}
