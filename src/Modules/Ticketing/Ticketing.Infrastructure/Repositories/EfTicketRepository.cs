using Microsoft.EntityFrameworkCore;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Domain.Entities;
using BuildingBlocks.Infrastructure.Persistence;
using Ticketing.Infrastructure.Persistence;

namespace Ticketing.Infrastructure.Repositories;

public sealed class EfTicketRepository(TicketingDbContext context) : ITicketRepository
{
    public async Task<Ticket?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await context.Tickets.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

    public async Task<Ticket?> GetIncluyendoOcultosAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        // El ámbito se cierra al salir del `using`. No vale `IgnoreQueryFilters()`: apagaría
        // también el aislamiento por inquilino.
        using var _ = context.VerTambien(borrados: true, archivados: true);

        return await context.Tickets.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);
    }

    public async Task AddAsync(Ticket ticket, CancellationToken ct = default)
        => await context.Tickets.AddAsync(ticket, ct);

    public Task UpdateAsync(Ticket ticket, CancellationToken ct = default)
    {
        context.Tickets.Update(ticket);
        return Task.CompletedTask;
    }
}
