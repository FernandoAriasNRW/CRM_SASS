using Microsoft.EntityFrameworkCore;
using Ticketing.Application.Intake;
using Ticketing.Domain.Entities;
using Ticketing.Infrastructure.Persistence;

namespace Ticketing.Infrastructure.Repositories;

public sealed class EfIntakeKeyRepository(TicketingDbContext context) : IIntakeKeyRepository
{
    public Task<IntakeKey?> FindActiveByHashAsync(string hash, CancellationToken ct)
        // IgnoreQueryFilters sólo apaga filtros de esta tabla, que no tiene papelera ni archivo:
        // lo único que se salta es el de inquilino, y es lo que hace falta. La clave es la que
        // dice de qué organización es la petición.
        => context.IntakeKeys.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Hash == hash && c.RevokedAtUtc == null, ct);

    public Task<List<IntakeKey>> GetByTenantAsync(Guid tenantId, CancellationToken ct)
        => context.IntakeKeys.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.RevokedAtUtc != null).ThenByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

    public Task<IntakeKey?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
        => context.IntakeKeys.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public async Task AddAsync(IntakeKey key, CancellationToken ct)
        => await context.IntakeKeys.AddAsync(key, ct);
}
