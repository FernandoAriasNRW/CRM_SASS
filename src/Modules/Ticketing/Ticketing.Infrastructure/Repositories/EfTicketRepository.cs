using Microsoft.EntityFrameworkCore;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using Ticketing.Infrastructure.Persistence;

namespace Ticketing.Infrastructure.Repositories;

public sealed class EfTicketRepository(TicketingDbContext context) : ITicketRepository
{
    public async Task<Ticket?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await context.Tickets.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

    public async Task AddAsync(Ticket ticket, CancellationToken ct = default)
        => await context.Tickets.AddAsync(ticket, ct);

    public Task UpdateAsync(Ticket ticket, CancellationToken ct = default)
    {
        context.Tickets.Update(ticket);
        return Task.CompletedTask;
    }
}
