using Microsoft.EntityFrameworkCore;
using Reporting.Application.Programaciones;
using Reporting.Domain.Entities;

namespace Reporting.Infrastructure.Persistence;

public sealed class RepositorioDeProgramaciones(ReportingDbContext contexto) : IRepositorioDeProgramaciones
{
    public Task<ProgramacionDeInforme?> PorIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => contexto.Programaciones.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public async Task<IReadOnlyList<ProgramacionDeInforme>> DelInformeAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default)
        => await contexto.Programaciones.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.ReportId == reportId)
            .OrderBy(p => p.Hora)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProgramacionDeInforme>> ActivasAsync(CancellationToken ct = default)
        // `IgnoreQueryFilters` a conciencia: el trabajador corre sin petición, así que el filtro
        // de inquilino compararía contra Guid.Empty y no casaría con ninguna fila. Cada
        // programación lleva su TenantId y todo lo que se hace con ella lo usa.
        => await contexto.Programaciones
            .IgnoreQueryFilters()
            .Where(p => p.Activa)
            .ToListAsync(ct);

    public async Task AnadirAsync(ProgramacionDeInforme programacion, CancellationToken ct = default)
        => await contexto.Programaciones.AddAsync(programacion, ct);

    public void Quitar(ProgramacionDeInforme programacion) => contexto.Programaciones.Remove(programacion);

    public Task GuardarAsync(CancellationToken ct = default) => contexto.SaveChangesAsync(ct);
}
