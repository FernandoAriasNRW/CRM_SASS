using Ticketing.Domain.Entities;

namespace Ticketing.Application.Abstractions.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task AddAsync(Ticket ticket, CancellationToken ct = default);

    Task UpdateAsync(Ticket ticket, CancellationToken ct = default);
}
