using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Repositories;

public interface IEntityPermissionRepository
{
    Task<List<EntityPermission>> GetPermissionsAsync(
        Guid tenantId,
        string? targetType = null,
        Guid? targetId = null,
        string? roleName = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(EntityPermission permission, CancellationToken cancellationToken = default);
}
