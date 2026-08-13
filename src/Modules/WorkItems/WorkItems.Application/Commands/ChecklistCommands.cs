using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using WorkItems.Application.DTOs;

namespace WorkItems.Application.Commands;

public sealed record AddChecklistItemCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole,
    string Texto
) : ICommand<ChecklistItemDto>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.checklist.added";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}

/// <summary>
/// Marca, desmarca o renombra un punto.
///
/// Los dos campos son opcionales y se distingue «no lo toques» de «cámbialo» por el nulo, que
/// aquí sí funciona: ninguno de los dos tiene el nulo como valor legítimo.
/// </summary>
public sealed record UpdateChecklistItemCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole,
    Guid ItemId,
    bool? Hecho,
    string? Texto
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.checklist.updated";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}

public sealed record RemoveChecklistItemCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole,
    Guid ItemId
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.checklist.removed";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}
