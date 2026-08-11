using Microsoft.EntityFrameworkCore;
using Projects.Application.Abstractions.Repositories;
using Projects.Domain.Entities;
using Projects.Infrastructure.Persistence;

namespace Projects.Infrastructure.Repositories;

internal sealed class EfFolderRepository(ProjectsDbContext dbContext) : IFolderRepository
{
    public async Task<Folder?> GetByIdAsync(Guid tenantId, Guid folderId, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Folders.Where(f => f.TenantId == tenantId && f.Id == folderId);
        if (includeDeleted) query = query.IgnoreQueryFilters();
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<Folder>> GetAllAsync(Guid tenantId, Guid spaceId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Folders
            .Where(f => f.TenantId == tenantId && f.SpaceId == spaceId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        await dbContext.Folders.AddAsync(folder, cancellationToken);
    }

    public Task UpdateAsync(Folder folder, CancellationToken cancellationToken = default)
    {
        dbContext.Folders.Update(folder);
        return Task.CompletedTask;
    }
}
