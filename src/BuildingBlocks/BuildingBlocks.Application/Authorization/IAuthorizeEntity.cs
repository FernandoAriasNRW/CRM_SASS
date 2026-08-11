namespace BuildingBlocks.Application.Authorization;

/// <summary>
/// Interfaz para marcar peticiones MediatR que requieren autorización de entidad.
/// </summary>
public interface IAuthorizeEntity
{
    Guid TenantId { get; }
    string EntityType { get; }
    Guid EntityId { get; }
    string RequiredPermission { get; } // Read, Write, Admin
}
