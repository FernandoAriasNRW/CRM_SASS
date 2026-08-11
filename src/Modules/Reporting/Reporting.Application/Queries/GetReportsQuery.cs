using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.DTOs;

namespace Reporting.Application.Queries;

public sealed record GetReportsQuery(
    Guid TenantId,
    string? Type,
    PaginationRequest Pagination
) : IQuery<PagedResult<ReportDto>>;
