using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;

namespace WorkItems.Application.Commands;

/// <summary>
/// Hace que una tarea se repita, o cambia cada cuánto.
/// </summary>
public sealed record SetTaskRecurrenceCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole,
    string Frecuencia,
    int Intervalo,
    DateOnly? ProximaOcurrencia,
    DateOnly? FechaFin
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.recurrence.set";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}

public sealed record ClearTaskRecurrenceCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.recurrence.cleared";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}
