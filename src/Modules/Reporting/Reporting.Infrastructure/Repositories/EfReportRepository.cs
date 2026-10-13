using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Application.DTOs;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Repositories;

public sealed class EfReportRepository(ReportingDbContext context) : IReportRepository
{
    public async Task<(IReadOnlyList<ReportDto> Items, int TotalCount)> GetByTenantAsync(
        Guid tenantId, string? type, PaginationRequest pagination, CancellationToken ct = default, string? format = null)
    {
        var query = context.Reports.AsNoTracking().Where(r => r.TenantId == tenantId);

        // Por el número guardado, no por el nombre: `Type` y `Format` se calculan a partir de
        // él y EF no los traduce a SQL, así que filtrar por `r.Type.Name` reventaba la consulta.
        // Un nombre que no existe da una lista vacía, no todos los informes.
        if (!string.IsNullOrEmpty(type))
        {
            var typeValue = ReportType.FromName<ReportType>(type)?.Value ?? -1;
            query = query.Where(r => r.TypeValue == typeValue);
        }

        if (!string.IsNullOrEmpty(format))
        {
            var formatValue = ReportFormat.FromName<ReportFormat>(format)?.Value ?? -1;
            query = query.Where(r => r.FormatValue == formatValue);
        }

        if (pagination.SearchText is { } text)
            query = query.Where(r => r.Name.Contains(text));

        var totalCount = await query.CountAsync(ct);

        var desc = pagination.SortDirection?.ToLower() == "desc";
        IOrderedQueryable<Report> ordered = pagination.SortColumn?.ToLower() switch
        {
            "name" => desc ? query.OrderByDescending(r => r.Name) : query.OrderBy(r => r.Name),
            "type" => desc ? query.OrderByDescending(r => r.TypeValue) : query.OrderBy(r => r.TypeValue),
            "format" => desc ? query.OrderByDescending(r => r.FormatValue) : query.OrderBy(r => r.FormatValue),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var items = await ordered
            // Desempate por Id: ver TicketQueries. Sin él, al paginar una fila puede repetirse.
            .ThenBy(r => r.Id)
            .Skip(pagination.Skip).Take(pagination.Take)
            .Select(r => ReportDto.FromEntity(r))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Report?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await context.Reports.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

    public async Task<Report> AddAsync(Report report, CancellationToken ct = default)
    {
        context.Reports.Add(report);
        await context.SaveChangesAsync(ct);
        return report;
    }

    public async Task<bool> UpdateAsync(Report report, CancellationToken ct = default)
    {
        context.Reports.Update(report);
        await context.SaveChangesAsync(ct);
        return true;
    }
}
