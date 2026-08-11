using Microsoft.EntityFrameworkCore;
using Reporting.Application.Dashboards;
using Reporting.Domain.Entities;
using Reporting.Infrastructure.Persistence;

namespace Reporting.Infrastructure.Repositories;

internal sealed class CustomDashboardRepository : ICustomDashboardRepository
{
    private readonly ReportingDbContext _dbContext;

    public CustomDashboardRepository(ReportingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Dashboard dashboard, CancellationToken cancellationToken = default)
    {
        await _dbContext.Dashboards.AddAsync(dashboard, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Dashboard>> GetDashboardsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Dashboards
            .Where(d => d.TenantId == tenantId && (d.IsPublic || d.CreatedById == userId))
            .ToListAsync(cancellationToken);
    }

    public async Task<Dashboard?> GetByIdAsync(Guid tenantId, Guid dashboardId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Dashboards
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == dashboardId, cancellationToken);
    }

    public async Task UpdateAsync(Dashboard dashboard, CancellationToken cancellationToken = default)
    {
        _dbContext.Dashboards.Update(dashboard);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Dashboard dashboard, CancellationToken cancellationToken = default)
    {
        _dbContext.Dashboards.Remove(dashboard);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
