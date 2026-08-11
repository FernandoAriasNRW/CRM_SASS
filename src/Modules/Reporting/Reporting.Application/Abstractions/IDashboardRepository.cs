using Reporting.Application.DTOs;

namespace Reporting.Application.Abstractions;

public interface IDashboardRepository
{
    Task<KpiDataDto> GetKpiDataAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<List<TaskStatusBreakdownDto>> GetTaskBreakdownAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<List<ProjectProgressDto>> GetProjectProgressAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<ProjectBurndownDto> GetProjectBurndownAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken);
}
