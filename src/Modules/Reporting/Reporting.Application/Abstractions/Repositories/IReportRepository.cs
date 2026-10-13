using BuildingBlocks.Domain;
using Reporting.Application.DTOs;
using Reporting.Domain.Entities;

namespace Reporting.Application.Abstractions.Repositories;

public interface IReportRepository
{
    Task<(IReadOnlyList<ReportDto> Items, int TotalCount)> GetByTenantAsync(
        Guid tenantId,
        string? type,
        PaginationRequest pagination,
        CancellationToken ct = default,
        string? format = null);

    Task<Report?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<Report> AddAsync(Report report, CancellationToken ct = default);

    Task<bool> UpdateAsync(Report report, CancellationToken ct = default);
}