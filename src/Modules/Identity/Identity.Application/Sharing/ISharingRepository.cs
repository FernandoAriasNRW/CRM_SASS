using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;

namespace Identity.Application.Sharing;

/// <summary>
/// Acceso a las filas de permiso que representan una compartición nominal.
///
/// Sólo mira filas con <c>EntityId</c> real y <c>UserId</c> real: los permisos por rol y los de
/// módulo entero viven en la misma tabla y no son comparticiones.
/// </summary>
public interface ISharingRepository
{
    Task<EntityPermission?> FindAsync(Guid tenantId, Guid userId, string permissionType, Guid entityId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> GetSharedWithUsersAsync(Guid tenantId, string permissionType, Guid entityId, CancellationToken ct);

    Task<IReadOnlyList<Guid>> GetSharedWithUserAsync(Guid tenantId, Guid userId, string permissionType, CancellationToken ct);

    Task<IReadOnlyList<Guid>> GetSharedWithOthersAsync(Guid tenantId, string permissionType, CancellationToken ct);

    Task AddAsync(EntityPermission permission, CancellationToken ct);

    void Remove(EntityPermission permission);
}
