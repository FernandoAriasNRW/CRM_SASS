using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

/// <summary>Adjuntar imágenes o vídeos a un ticket desde la aplicación.</summary>
public sealed record UploadTicketAttachmentsCommand(
    Guid TicketId, Guid UploadedBy, IReadOnlyList<IncomingFile> Attachments)
    : ICommand<List<TicketAttachmentDto>>, IAuthorizeEntity
{
    public string EntityType => "Ticket";
    public Guid EntityId => TicketId;
    public string RequiredPermission => "Write";
}
