using Microsoft.EntityFrameworkCore;
using Projects.Application.Abstractions.Repositories;
using Projects.Domain.Entities;
using Projects.Infrastructure.Persistence;

namespace Projects.Infrastructure.Repositories;

internal sealed class EfSpaceRepository(ProjectsDbContext dbContext) : ISpaceRepository
{
    public async Task<Space?> GetByIdAsync(Guid tenantId, Guid spaceId, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Spaces.Where(s => s.TenantId == tenantId && s.Id == spaceId);
        if (includeDeleted) query = query.IgnoreQueryFilters();
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<Space>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Spaces
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Space space, CancellationToken cancellationToken = default)
    {
        await dbContext.Spaces.AddAsync(space, cancellationToken);
    }

    public Task UpdateAsync(Space space, CancellationToken cancellationToken = default)
    {
        dbContext.Spaces.Update(space);
        return Task.CompletedTask;
    }
}
