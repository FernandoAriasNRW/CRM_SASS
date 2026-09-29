using Microsoft.EntityFrameworkCore;
using Reporting.Application.Schedules;
using Reporting.Domain.Entities;

namespace Reporting.Infrastructure.Persistence;

public sealed class ScheduleRepository(ReportingDbContext db) : IScheduleRepository
{
    public Task<ReportSchedule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => db.ReportSchedules.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public async Task<IReadOnlyList<ReportSchedule>> ForReportAsync(
        Guid tenantId, Guid reportId, CancellationToken ct = default)
        => await db.ReportSchedules.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.ReportId == reportId)
            .OrderBy(p => p.Time)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ReportSchedule>> ActiveAsync(CancellationToken ct = default)
        // `IgnoreQueryFilters` a conciencia: el trabajador corre sin petición, así que el filtro
        // de inquilino compararía contra Guid.Empty y no casaría con ninguna fila. Cada
        // programación lleva su TenantId y todo lo que se hace con ella lo usa.
        => await db.ReportSchedules
            .IgnoreQueryFilters()
            .Where(p => p.IsActive)
            .ToListAsync(ct);

    public async Task AddAsync(ReportSchedule schedule, CancellationToken ct = default)
        => await db.ReportSchedules.AddAsync(schedule, ct);

    public void Remove(ReportSchedule schedule) => db.ReportSchedules.Remove(schedule);

    public Task SaveAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
