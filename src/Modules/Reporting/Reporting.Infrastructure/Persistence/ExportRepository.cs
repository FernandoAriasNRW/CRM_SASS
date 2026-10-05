using Microsoft.EntityFrameworkCore;
using Reporting.Application.Exports;
using Reporting.Domain.Entities;

namespace Reporting.Infrastructure.Persistence;

public sealed class ExportRepository(TimeProvider timeProvider, ReportingDbContext db) : IExportRepository
{
    public Task<Export?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => db.Exports.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id, ct);

    public async Task<IReadOnlyList<Export>> ForReportAsync(Guid tenantId, Guid reportId, CancellationToken ct = default)
        => await db.Exports.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.ReportId == reportId)
            .OrderByDescending(e => e.RequestedAtUtc)
            .ToListAsync(ct);

    public Task<Export?> InProgressAsync(
        Guid tenantId, Guid reportId, int formatValue, Guid requestedById, CancellationToken ct = default)
        => db.Exports.AsNoTracking()
            .Where(e => e.TenantId == tenantId
                        && e.ReportId == reportId
                        && e.FormatValue == formatValue
                        && e.RequestedById == requestedById
                        && (e.StatusValue == 1 || e.StatusValue == 2))
            .OrderByDescending(e => e.RequestedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Export>> PendingAsync(int count, CancellationToken ct = default)
    {
        // `IgnoreQueryFilters` a conciencia: el trabajador corre sin petición, así que el filtro
        // de inquilino compara contra Guid.Empty y no casaría con ninguna fila. Es el caso que
        // TenantDbContext describe como legítimo, y por eso está encerrado aquí: el trabajador no
        // escribe consultas, pide trabajo.
        //
        // Cada Exportacion lleva su TenantId, y todo lo que el trabajador haga después con ella
        // usa ese identificador. La frontera no se pierde, se lleva a mano.
        var limit = timeProvider.GetUtcNow().UtcDateTime - Export.GivenUpAfter;

        return await db.Exports
            .IgnoreQueryFilters()
            .Where(e => e.StatusValue == 1
                        // Las colgadas: en «generando» desde hace demasiado y con intentos de
                        // sobra. Sin esto, una caída del proceso deja la exportación esperando
                        // para siempre, que es justo lo que el plan señalaba como lo peor.
                        || (e.StatusValue == 2
                            && e.Attempts < Export.MaxAttempts
                            && e.StartedAtUtc != null
                            && e.StartedAtUtc < limit))
            .OrderBy(e => e.RequestedAtUtc)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Export export, CancellationToken ct = default)
        => await db.Exports.AddAsync(export, ct);

    public async Task SaveContentAsync(ExportContent content, CancellationToken ct = default)
        => await db.ExportContents.AddAsync(content, ct);

    public Task<ExportContent?> ContentAsync(Guid tenantId, Guid exportId, CancellationToken ct = default)
        => db.ExportContents.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ExportId == exportId, ct);

    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
