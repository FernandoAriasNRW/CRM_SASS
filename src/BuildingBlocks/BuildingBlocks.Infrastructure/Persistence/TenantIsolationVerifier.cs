using BuildingBlocks.Domain.Primitives;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Comprueba que ninguna entidad se haya quedado fuera del aislamiento por tenant.
///
/// El filtro global sólo protege a quien implementa <c>ITenantEntity</c>. Una entidad
/// nueva con columna <c>TenantId</c> que olvide la interfaz, o un <c>DbContext</c> que
/// olvide llamar a <c>ApplyTenantFilters</c>, quedarían sin filtro y devolverían datos
/// de todos los clientes sin error visible.
///
/// Este verificador convierte ese olvido en un fallo de arranque. Se prefiere no
/// arrancar a servir datos cruzados.
/// </summary>
public static class TenantIsolationVerifier
{
    /// <summary>
    /// Devuelve las infracciones encontradas en el modelo. Vacío significa correcto.
    /// </summary>
    public static IReadOnlyList<string> FindViolations(DbContext context)
    {
        var violations = new List<string>();
        var contextName = context.GetType().Name;

        foreach (var entityType in context.Model.GetEntityTypes())
        {
            if (entityType.IsOwned())
                continue;

            var clrType = entityType.ClrType;
            var declaresTenantId = clrType.GetProperty(nameof(ITenantEntity.TenantId)) is not null;
            var implementsInterface = typeof(ITenantEntity).IsAssignableFrom(clrType);

            if (declaresTenantId && !implementsInterface)
            {
                violations.Add(
                    $"{contextName}.{clrType.Name} tiene propiedad TenantId pero no implementa ITenantEntity: " +
                    "sus consultas devolverían filas de todos los tenants.");
                continue;
            }

            if (implementsInterface && entityType.GetQueryFilter() is null)
            {
                violations.Add(
                    $"{contextName}.{clrType.Name} implementa ITenantEntity pero no tiene filtro global aplicado: " +
                    $"falta la llamada a ApplyTenantFilters al final de {contextName}.OnModelCreating.");
            }
        }

        return violations;
    }

    /// <summary>
    /// Lanza si el modelo tiene alguna entidad sin aislar.
    /// </summary>
    public static void Verify(DbContext context)
    {
        var violations = FindViolations(context);
        if (violations.Count == 0)
            return;

        throw new InvalidOperationException(
            "Aislamiento multi-tenant incompleto. La aplicación no arranca para evitar fuga de datos entre clientes:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations.Select(v => "  - " + v)));
    }
}
