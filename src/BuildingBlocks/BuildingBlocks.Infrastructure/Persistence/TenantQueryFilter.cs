using System.Linq.Expressions;
using BuildingBlocks.Domain.Primitives;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Aplica los filtros globales de aislamiento por tenant y de soft delete a todas las
/// entidades del modelo.
///
/// Ambos se componen en una única expresión a propósito: EF Core reemplaza el filtro
/// anterior en cada llamada a <c>HasQueryFilter</c>, de modo que declararlos por
/// separado dejaría activo sólo el último y desactivaría el aislamiento en silencio.
/// </summary>
public static class TenantQueryFilter
{
    /// <summary>
    /// Recorre el modelo y aplica a cada entidad el filtro que le corresponda según las
    /// interfaces que implemente.
    /// </summary>
    /// <param name="tenantIdAccessor">
    /// Se evalúa en cada consulta, no al construir el modelo. Debe leer del
    /// <c>DbContext</c> para que EF lo traduzca a un parámetro y no a una constante
    /// horneada en el modelo compilado y cacheado.
    /// </param>
    public static void ApplyGlobalFilters(ModelBuilder modelBuilder, Expression<Func<Guid>> tenantIdAccessor)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            // Los tipos owned heredan el filtro de su propietario; aplicarles uno propio
            // es un error de configuración en EF Core.
            if (entityType.IsOwned())
                continue;

            var isTenantScoped = typeof(ITenantEntity).IsAssignableFrom(clrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clrType);

            if (!isTenantScoped && !isSoftDeletable)
                continue;

            var parameter = Expression.Parameter(clrType, "e");
            Expression? body = null;

            if (isTenantScoped)
            {
                body = Expression.Equal(
                    Expression.Property(parameter, nameof(ITenantEntity.TenantId)),
                    tenantIdAccessor.Body);
            }

            if (isSoftDeletable)
            {
                var notDeleted = Expression.Not(
                    Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted)));

                body = body is null ? notDeleted : Expression.AndAlso(body, notDeleted);
            }

            modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(body!, parameter));
        }
    }
}
