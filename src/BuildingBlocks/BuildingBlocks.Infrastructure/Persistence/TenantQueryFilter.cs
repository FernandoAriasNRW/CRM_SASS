using System.Linq.Expressions;
using BuildingBlocks.Domain.Primitives;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Aplica los filtros globales —aislamiento por tenant, papelera y archivado— a todas las
/// entidades del modelo.
///
/// Los tres se componen en una única expresión a propósito: EF Core reemplaza el filtro
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
    /// <param name="incluirBorradosAccessor">
    /// Deja pasar lo que está en la papelera. Mismo tratamiento que el tenant: se lee del
    /// contexto en cada consulta, así que el modelo cacheado sirve para las dos vistas.
    /// </param>
    /// <param name="incluirArchivadosAccessor">Deja pasar lo archivado.</param>
    public static void ApplyGlobalFilters(
        ModelBuilder modelBuilder,
        Expression<Func<Guid>> tenantIdAccessor,
        Expression<Func<bool>> incluirBorradosAccessor,
        Expression<Func<bool>> incluirArchivadosAccessor)
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
            var isArchivable = typeof(IArchivable).IsAssignableFrom(clrType);

            if (!isTenantScoped && !isSoftDeletable && !isArchivable)
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
                // «No está borrado, o me han pedido ver la papelera». El segundo término es un
                // parámetro, no una constante: no puede desaparecer del SQL ni dejar el filtro
                // horneado en el modelo compilado.
                var visible = Expression.OrElse(
                    Expression.Not(Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted))),
                    incluirBorradosAccessor.Body);

                body = body is null ? visible : Expression.AndAlso(body, visible);
            }

            if (isArchivable)
            {
                var visible = Expression.OrElse(
                    Expression.Equal(
                        Expression.Property(parameter, nameof(IArchivable.ArchivadoEnUtc)),
                        Expression.Constant(null, typeof(DateTime?))),
                    incluirArchivadosAccessor.Body);

                body = body is null ? visible : Expression.AndAlso(body, visible);
            }

            modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(body!, parameter));
        }
    }
}
