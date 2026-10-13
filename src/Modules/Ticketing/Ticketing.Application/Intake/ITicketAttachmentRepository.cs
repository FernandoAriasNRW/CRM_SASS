using System.Net.Mail;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Domain.ValueObjects;

namespace Ticketing.Application.Intake;

public interface ITicketAttachmentRepository
{
    Task<List<TicketAttachment>> GetByTicketAsync(Guid tenantId, Guid ticketId, CancellationToken ct);
    Task AddAsync(TicketAttachment attachment, CancellationToken ct);

    /// <summary>Un adjunto de un ticket, para quitarlo; <c>null</c> si no es de ese ticket.</summary>
    Task<TicketAttachment?> GetAsync(Guid tenantId, Guid ticketId, Guid attachmentId, CancellationToken ct);

    void Remove(TicketAttachment attachment);
}
