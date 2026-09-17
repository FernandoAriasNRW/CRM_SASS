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
}
