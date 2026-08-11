using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Base de los <c>DbContext</c> de módulo. Aplica automáticamente el filtro de tenant y
/// el de soft delete a toda entidad que implemente <c>ITenantEntity</c> o
/// <c>ISoftDeletable</c>.
///
/// Heredar de aquí es lo que sustituye al filtrado manual por <c>TenantId</c> repetido
/// en cada consulta: el aislamiento pasa a ser el comportamiento por defecto en lugar de
/// algo que cada handler debe acordarse de hacer.
/// </summary>
public abstract class TenantDbContext(DbContextOptions options, IUserContext? userContext) : DbContext(options)
{
    /// <summary>
    /// Tenant de la petición en curso. Se lee en cada consulta, no al construir el
    /// modelo, para que EF lo traduzca a un parámetro y el modelo cacheado sirva a
    /// todos los tenants.
    ///
    /// Sin contexto de usuario (workers en segundo plano, herramientas de diseño)
    /// devuelve <c>Guid.Empty</c>, que no casa con ninguna fila: el filtro cierra por
    /// defecto. Un proceso que legítimamente deba cruzar tenants ha de declararlo
    /// explícitamente con <c>IgnoreQueryFilters()</c>.
    /// </summary>
    public Guid CurrentTenantId => userContext?.TenantId ?? Guid.Empty;

    /// <summary>
    /// Debe invocarse al final de <c>OnModelCreating</c> en las clases derivadas, después
    /// de <c>ApplyConfigurationsFromAssembly</c> y de cualquier <c>ComplexProperty</c>:
    /// el filtro recorre el modelo ya construido y necesita ver todas las entidades
    /// registradas. Aplicarlo antes dejaría entidades sin filtro.
    ///
    /// <c>TenantIsolationVerifier</c> comprueba al arrancar que ninguna se haya quedado
    /// fuera, de modo que olvidar esta llamada rompe el arranque en vez de filtrar datos.
    /// </summary>
    protected void ApplyTenantFilters(ModelBuilder modelBuilder)
        => TenantQueryFilter.ApplyGlobalFilters(modelBuilder, () => CurrentTenantId);
}
