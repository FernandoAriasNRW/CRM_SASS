using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Application.DTOs;
using Reporting.Application.Queries;

namespace Reporting.Application.Handlers.Queries;

public sealed class GetReportByIdHandler(IReportRepository repository) : IQueryHandler<GetReportByIdQuery, ReportDto?>
{
  private readonly IReportRepository _repository = repository;

  public async Task<Result<ReportDto?>> Handle(GetReportByIdQuery request, CancellationToken cancellationToken)
  {
    var report = await _repository.GetByIdAsync(request.TenantId, request.ReportId, cancellationToken);

    if (report is null)
    {
      return Result<ReportDto?>.Failure("Report not found.");
    }

    var dto = ReportDto.FromEntity(report);

    return Result<ReportDto?>.Success(dto);
  }
}

public sealed class GetReportsHandler(IReportRepository repository) : IQueryHandler<GetReportsQuery, PagedResult<ReportDto>>
{
  private readonly IReportRepository _repository = repository;

  public async Task<Result<PagedResult<ReportDto>>> Handle(GetReportsQuery request, CancellationToken cancellationToken)
  {
    var (items, totalCount) = await _repository.GetByTenantAsync(request.TenantId, request.Type, request.Pagination, cancellationToken);
    var result = PagedResult<ReportDto>.Create(items, request.Pagination.Page, request.Pagination.PageSize, totalCount);
    return Result<PagedResult<ReportDto>>.Success(result);
  }
}