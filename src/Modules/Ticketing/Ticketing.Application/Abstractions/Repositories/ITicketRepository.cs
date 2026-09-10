using Ticketing.Domain.Entities;

namespace Ticketing.Application.Abstractions.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Busca el ticket aunque esté archivado o en la papelera.
    ///
    /// Existe porque el filtro global esconde las dos cosas y restaurar algo exige poder
    /// encontrarlo primero. Es la excepción, no la norma.
    /// </summary>
    Task<Ticket?> GetIncluyendoOcultosAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task AddAsync(Ticket ticket, CancellationToken ct = default);

    Task UpdateAsync(Ticket ticket, CancellationToken ct = default);
}
