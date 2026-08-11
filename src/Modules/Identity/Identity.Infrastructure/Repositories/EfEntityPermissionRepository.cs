using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public sealed class EfEntityPermissionRepository(IdentityDbContext dbContext) : IEntityPermissionRepository
{
    public async Task<List<EntityPermission>> GetPermissionsAsync(
        Guid tenantId,
        string? targetType = null,
        Guid? targetId = null,
        string? roleName = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.EntityPermissions
            .Where(p => p.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(targetType))
        {
            query = query.Where(p => p.TargetType == targetType);
        }

        if (targetId.HasValue && targetId.Value != Guid.Empty)
        {
            query = query.Where(p => p.UserId == targetId || p.TeamId == targetId);
        }

        if (!string.IsNullOrWhiteSpace(roleName))
        {
            query = query.Where(p => p.RoleName == roleName);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(EntityPermission permission, CancellationToken cancellationToken = default)
    {
        await dbContext.EntityPermissions.AddAsync(permission, cancellationToken);
    }
}
