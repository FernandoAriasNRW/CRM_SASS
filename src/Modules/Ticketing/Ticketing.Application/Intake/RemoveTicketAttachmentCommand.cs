using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;

namespace Ticketing.Application.Intake;

/// <summary>Quitar un adjunto de un ticket: la fila y su fichero en el almacenamiento.</summary>
public sealed record RemoveTicketAttachmentCommand(Guid TicketId, Guid AttachmentId)
    : ICommand<bool>, IAuthorizeEntity
{
    public string EntityType => "Ticket";
    public Guid EntityId => TicketId;
    public string RequiredPermission => "Write";
}
