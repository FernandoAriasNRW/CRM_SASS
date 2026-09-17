using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Sharing;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Las comparticiones nominales, sacadas de la tabla de permisos por entidad.
///
/// Todas las consultas exigen <c>UserId != null</c> y <c>EntityId != Guid.Empty</c>: en la misma
/// tabla conviven los permisos por rol y los de módulo entero, y contarlos como comparticiones
/// haría que «compartido conmigo» devolviera media aplicación —otra entrada de menú que promete
/// y no filtra—.
/// </summary>
public sealed class SharingRepository(IdentityDbContext context) : ISharingRepository
{
    private IQueryable<EntityPermission> UserSpecificGrants(Guid tenantId, string permissionType)
        => context.EntityPermissions
            .Where(p => p.TenantId == tenantId
                        && p.EntityType == permissionType
                        && p.UserId != null
                        && p.EntityId != Guid.Empty
                        && p.PermissionLevel != "None");

    public Task<EntityPermission?> FindAsync(Guid tenantId, Guid userId, string permissionType, Guid entityId, CancellationToken ct)
        => context.EntityPermissions.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.UserId == userId
                 && p.EntityType == permissionType && p.EntityId == entityId, ct);

    public async Task<IReadOnlyList<Guid>> GetSharedWithUsersAsync(Guid tenantId, string permissionType, Guid entityId, CancellationToken ct)
        => await UserSpecificGrants(tenantId, permissionType).AsNoTracking()
            .Where(p => p.EntityId == entityId)
            .Select(p => p.UserId!.Value)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetSharedWithUserAsync(Guid tenantId, Guid userId, string permissionType, CancellationToken ct)
        => await UserSpecificGrants(tenantId, permissionType).AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.EntityId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetSharedWithOthersAsync(Guid tenantId, string permissionType, CancellationToken ct)
        => await UserSpecificGrants(tenantId, permissionType).AsNoTracking()
            .Select(p => p.EntityId)
            .Distinct()
            .ToListAsync(ct);

    public async Task AddAsync(EntityPermission permission, CancellationToken ct)
        => await context.EntityPermissions.AddAsync(permission, ct);

    public void Remove(EntityPermission permission) => context.EntityPermissions.Remove(permission);
}
