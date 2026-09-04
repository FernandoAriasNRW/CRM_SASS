using Microsoft.EntityFrameworkCore;
using Reporting.Application.Exportaciones;
using Reporting.Domain.Entities;

namespace Reporting.Infrastructure.Persistence;

public sealed class RepositorioDeExportaciones(ReportingDbContext contexto) : IRepositorioDeExportaciones
{
    public Task<Exportacion?> PorIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => contexto.Exportaciones.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, ct);

    public async Task<IReadOnlyList<Exportacion>> DelInformeAsync(Guid tenantId, Guid reportId, CancellationToken ct = default)
        => await contexto.Exportaciones.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.ReportId == reportId)
            .OrderByDescending(e => e.SolicitadaUtc)
            .ToListAsync(ct);

    public Task<Exportacion?> EnMarchaAsync(
        Guid tenantId, Guid reportId, int formatoValue, Guid solicitadaPorId, CancellationToken ct = default)
        => contexto.Exportaciones.AsNoTracking()
            .Where(e => e.TenantId == tenantId
                        && e.ReportId == reportId
                        && e.FormatoValue == formatoValue
                        && e.SolicitadaPorId == solicitadaPorId
                        && (e.EstadoValue == 1 || e.EstadoValue == 2))
            .OrderByDescending(e => e.SolicitadaUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Exportacion>> PendientesAsync(int cuantas, CancellationToken ct = default)
    {
        // `IgnoreQueryFilters` a conciencia: el trabajador corre sin petición, así que el filtro
        // de inquilino compara contra Guid.Empty y no casaría con ninguna fila. Es el caso que
        // TenantDbContext describe como legítimo, y por eso está encerrado aquí: el trabajador no
        // escribe consultas, pide trabajo.
        //
        // Cada Exportacion lleva su TenantId, y todo lo que el trabajador haga después con ella
        // usa ese identificador. La frontera no se pierde, se lleva a mano.
        var limite = DateTime.UtcNow - Exportacion.SeDaPorPerdidaTras;

        return await contexto.Exportaciones
            .IgnoreQueryFilters()
            .Where(e => e.EstadoValue == 1
                        // Las colgadas: en «generando» desde hace demasiado y con intentos de
                        // sobra. Sin esto, una caída del proceso deja la exportación esperando
                        // para siempre, que es justo lo que el plan señalaba como lo peor.
                        || (e.EstadoValue == 2
                            && e.Intentos < Exportacion.IntentosMaximos
                            && e.ComenzadaUtc != null
                            && e.ComenzadaUtc < limite))
            .OrderBy(e => e.SolicitadaUtc)
            .Take(cuantas)
            .ToListAsync(ct);
    }

    public async Task AnadirAsync(Exportacion exportacion, CancellationToken ct = default)
        => await contexto.Exportaciones.AddAsync(exportacion, ct);

    public async Task GuardarContenidoAsync(ContenidoDeExportacion contenido, CancellationToken ct = default)
        => await contexto.ContenidosDeExportacion.AddAsync(contenido, ct);

    public Task<ContenidoDeExportacion?> ContenidoAsync(Guid tenantId, Guid exportacionId, CancellationToken ct = default)
        => contexto.ContenidosDeExportacion.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ExportacionId == exportacionId, ct);

    public Task GuardarAsync(CancellationToken ct = default) => contexto.SaveChangesAsync(ct);
}
