namespace BuildingBlocks.Application.Authorization;

/// <summary>
/// Interfaz para marcar peticiones MediatR que requieren autorización de entidad.
/// </summary>
public interface IAuthorizeEntity
{
    /// <summary>
    /// El inquilino lo pone el comportamiento a partir de la sesión cuando la petición no lo
    /// lleva, como los comandos de páginas de Documentos. Fiarse del que venga en la petición
    /// sería fiarse del cliente.
    /// </summary>
    Guid TenantId => Guid.Empty;
    string EntityType { get; }
    Guid EntityId { get; }
    string RequiredPermission { get; } // Read, Write, Admin
}
