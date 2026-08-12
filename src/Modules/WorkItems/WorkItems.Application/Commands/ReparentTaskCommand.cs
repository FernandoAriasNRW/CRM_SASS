using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;

namespace WorkItems.Application.Commands;

/// <summary>
/// Cuelga una tarea de otra, o la desliga.
///
/// Va en un comando propio y no en el patch general porque ahí un <c>Guid?</c> no distingue
/// «no cambies el padre» de «déjala sin padre»: los dos llegan como <c>null</c>. Aquí el
/// <c>null</c> significa siempre desligar, sin ambigüedad.
/// </summary>
public sealed record ReparentTaskCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    string ActorRole,
    Guid? ParentTaskId
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "workitem.reparented";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}
