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
        Guid tenantId, string? type, PaginationRequest pagination, CancellationToken ct = default)
    {
        var query = context.Reports.AsNoTracking().Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(r => r.Type.Name == type);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
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
