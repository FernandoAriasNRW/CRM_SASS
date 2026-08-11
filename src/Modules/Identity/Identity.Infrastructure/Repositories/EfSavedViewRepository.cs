using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public sealed class EfSavedViewRepository(IdentityDbContext context) : ISavedViewRepository
{
    public async Task<IReadOnlyList<SavedView>> GetByUserIdAsync(Guid tenantId, Guid userId, string moduleName, CancellationToken ct = default)
    {
        return await context.SavedViews
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserId == userId && x.ModuleName == moduleName)
            .ToListAsync(ct);
    }

    public async Task<SavedView?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await context.SavedViews
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    }

    public async Task AddAsync(SavedView savedView, CancellationToken ct = default)
    {
        await context.SavedViews.AddAsync(savedView, ct);
    }

    public void Update(SavedView savedView)
    {
        context.SavedViews.Update(savedView);
    }

    public Task UpdateAsync(SavedView savedView, CancellationToken ct = default)
    {
        context.SavedViews.Update(savedView);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SavedView savedView, CancellationToken ct = default)
    {
        context.SavedViews.Remove(savedView);
        return Task.CompletedTask;
    }
}
