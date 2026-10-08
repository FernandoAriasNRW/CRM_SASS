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
    decimal? EstimatedHours,
    DateOnly? StartDate = null,
    /// <summary>
    /// Vaciar la fecha de inicio. Hace falta un interruptor aparte porque `null` ya significa
    /// «no toques este campo», que es lo que necesita una pantalla que manda sólo lo que cambió.
    /// </summary>
    bool ClearStartDate = false,
    /// <summary>
    /// Las etiquetas, todas: sustituyen a las que tuviera. <c>null</c> es «no las toques»; una
    /// lista vacía, «quítalas todas».
    /// </summary>
    IReadOnlyList<Guid>? TagIds = null
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string WebhookEventName => "task.updated";

    public string EntityType => "Task";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}