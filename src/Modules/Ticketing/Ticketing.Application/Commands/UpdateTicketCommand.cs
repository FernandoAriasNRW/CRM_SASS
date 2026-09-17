using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;

namespace Ticketing.Application.Commands;

public sealed record UpdateTicketCommand(
    Guid TenantId,
    Guid TicketId,
    string? Title,
    string? Description,
    string? Priority,
    string? Status,
    Guid? AssignedAgentId,
    /// <summary>Como el resto: <c>null</c> deja el campo como estaba; cadena vacía lo borra.</summary>
    string? Classification = null,
    /// <summary><c>Guid.Empty</c> quita el equipo; <c>null</c> no lo toca.</summary>
    Guid? TeamId = null,
    /// <summary>
    /// Las etiquetas por su clave. <b>La ficha ya las mandaba y nadie las guardaba</b>: el
    /// comando no tenía dónde recibirlas, así que se perdían al cerrar el ticket.
    /// </summary>
    IReadOnlyList<string>? Tags = null
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string EntityType => "Ticket";
    public Guid EntityId => TicketId;
    public string RequiredPermission => "Write";

    public string WebhookEventName => "ticket.updated";
}