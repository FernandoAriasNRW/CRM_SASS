using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// Suelta una etiqueta borrada de los informes y dashboards que la llevan. Ver
/// <see cref="ITagReferences"/>. Los dos viven en el mismo contexto, así que es una sola
/// implementación y un solo guardado. En SQL y no en LINQ: ver <c>TaskTagReferences</c>.
/// </summary>
internal sealed class ReportingTagReferences(ReportingDbContext context) : ITagReferences
{
    public async Task<int> RemoveTagAsync(Guid tenantId, Guid tagId, CancellationToken ct = default)
    {
        var reports = await context.Reports
            .FromSqlInterpolated(
                $"SELECT * FROM `Reports` WHERE `TenantId` = {tenantId} AND JSON_CONTAINS(`TagIds`, JSON_QUOTE({tagId.ToString()}))")
            .IgnoreQueryFilters()
            .ToListAsync(ct);
        foreach (var report in reports)
            report.RemoveTag(tagId);

        var dashboards = await context.Dashboards
            .FromSqlInterpolated(
                $"SELECT * FROM `Dashboards` WHERE `TenantId` = {tenantId} AND JSON_CONTAINS(`TagIds`, JSON_QUOTE({tagId.ToString()}))")
            .IgnoreQueryFilters()
            .ToListAsync(ct);
        foreach (var dashboard in dashboards)
            dashboard.RemoveTag(tagId);

        await context.SaveChangesAsync(ct);
        return reports.Count + dashboards.Count;
    }
}
