using Microsoft.EntityFrameworkCore;
using Ticketing.Application.Intake;
using Ticketing.Domain.Entities;
using Ticketing.Infrastructure.Persistence;

namespace Ticketing.Infrastructure.Repositories;

public sealed class EfTicketAttachmentRepository(TicketingDbContext context) : ITicketAttachmentRepository
{
    public Task<List<TicketAttachment>> GetByTicketAsync(Guid tenantId, Guid ticketId, CancellationToken ct)
        => context.TicketAttachments.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.TicketId == ticketId)
            .OrderBy(a => a.UploadedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(TicketAttachment attachment, CancellationToken ct)
        => await context.TicketAttachments.AddAsync(attachment, ct);

    public Task<TicketAttachment?> GetAsync(Guid tenantId, Guid ticketId, Guid attachmentId, CancellationToken ct)
        => context.TicketAttachments
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.TicketId == ticketId && a.Id == attachmentId, ct);

    public void Remove(TicketAttachment attachment) => context.TicketAttachments.Remove(attachment);
}
