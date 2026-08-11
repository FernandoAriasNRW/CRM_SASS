namespace BuildingBlocks.Domain.Primitives;

/// <summary>
/// Marca una entidad como perteneciente a un tenant.
///
/// Implementarla es lo que hace que <c>TenantQueryFilter</c> aplique el filtro global
/// de aislamiento en el <c>DbContext</c>. Una entidad con columna <c>TenantId</c> que
/// no implemente esta interfaz queda fuera del filtro y sus consultas devuelven datos
/// de todos los tenants: por eso la aplicación verifica al arrancar que toda entidad
/// con esa propiedad la implemente (ver <c>TenantIsolationVerifier</c>).
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; }
}
