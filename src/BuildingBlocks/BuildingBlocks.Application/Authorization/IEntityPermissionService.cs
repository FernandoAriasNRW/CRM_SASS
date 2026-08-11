namespace BuildingBlocks.Application.Authorization;

/// <summary>
/// Interfaz para validar permisos de entidad a nivel de infraestructura.
/// </summary>
public interface IEntityPermissionService
{
    Task<bool> HasPermissionAsync(Guid tenantId, Guid userId, string entityType, Guid entityId, string requiredPermission, CancellationToken cancellationToken = default);
}
