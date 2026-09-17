using Microsoft.EntityFrameworkCore;
using Ticketing.Application.Entrada;
using Ticketing.Domain.Entities;
using Ticketing.Infrastructure.Persistence;

namespace Ticketing.Infrastructure.Repositories;

public sealed class EfClavesDeEntradaRepository(TicketingDbContext context) : IClavesDeEntradaRepository
{
    public Task<ClaveDeEntrada?> BuscarActivaPorHashAsync(string hash, CancellationToken ct)
        // IgnoreQueryFilters sólo apaga filtros de esta tabla, que no tiene papelera ni archivo:
        // lo único que se salta es el de inquilino, y es lo que hace falta. La clave es la que
        // dice de qué organización es la petición.
        => context.ClavesDeEntrada.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Hash == hash && c.RevocadaUtc == null, ct);

    public Task<List<ClaveDeEntrada>> DeLaOrganizacionAsync(Guid tenantId, CancellationToken ct)
        => context.ClavesDeEntrada.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.RevocadaUtc != null).ThenByDescending(c => c.CreadaUtc)
            .ToListAsync(ct);

    public Task<ClaveDeEntrada?> PorIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => context.ClavesDeEntrada.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public async Task AddAsync(ClaveDeEntrada clave, CancellationToken ct)
        => await context.ClavesDeEntrada.AddAsync(clave, ct);
}
