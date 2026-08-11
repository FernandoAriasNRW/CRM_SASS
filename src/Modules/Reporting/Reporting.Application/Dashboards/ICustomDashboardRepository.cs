using Reporting.Domain.Entities;

namespace Reporting.Application.Dashboards;

public interface ICustomDashboardRepository
{
    Task AddAsync(Dashboard dashboard, CancellationToken cancellationToken = default);
    Task<Dashboard?> GetByIdAsync(Guid tenantId, Guid dashboardId, CancellationToken cancellationToken = default);
    Task<List<Dashboard>> GetDashboardsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Dashboard dashboard, CancellationToken cancellationToken = default);
    Task DeleteAsync(Dashboard dashboard, CancellationToken cancellationToken = default);
}
