using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.DTOs;
using Reporting.Application.Queries;

namespace Reporting.Application.Handlers.Queries;

public sealed class DashboardQueryHandlers(IDashboardRepository repository) : 
    IQueryHandler<GetKpiDataQuery, KpiDataDto>,
    IQueryHandler<GetTaskBreakdownQuery, List<TaskStatusBreakdownDto>>,
    IQueryHandler<GetProjectProgressQuery, List<ProjectProgressDto>>,
    IQueryHandler<GetProjectBurndownQuery, ProjectBurndownDto>
{
  public async Task<Result<KpiDataDto>> Handle(GetKpiDataQuery request, CancellationToken cancellationToken)
  {
      var data = await repository.GetKpiDataAsync(request.TenantId, cancellationToken);
      return Result<KpiDataDto>.Success(data);
  }

  public async Task<Result<List<TaskStatusBreakdownDto>>> Handle(GetTaskBreakdownQuery request, CancellationToken cancellationToken)
  {
      var data = await repository.GetTaskBreakdownAsync(request.TenantId, cancellationToken);
      return Result<List<TaskStatusBreakdownDto>>.Success(data);
  }

  public async Task<Result<List<ProjectProgressDto>>> Handle(GetProjectProgressQuery request, CancellationToken cancellationToken)
  {
      var data = await repository.GetProjectProgressAsync(request.TenantId, cancellationToken);
      return Result<List<ProjectProgressDto>>.Success(data);
  }

  public async Task<Result<ProjectBurndownDto>> Handle(GetProjectBurndownQuery request, CancellationToken cancellationToken)
  {
      var data = await repository.GetProjectBurndownAsync(request.TenantId, request.ProjectId, cancellationToken);
      return Result<ProjectBurndownDto>.Success(data);
  }
}
