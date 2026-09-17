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
}
