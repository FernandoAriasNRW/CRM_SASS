using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// DbContext para el módulo Identity.
/// Configura el filtro global de soft delete y las configuraciones de entidad.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> User => Set<User>();
    public DbSet<SavedView> SavedViews => Set<SavedView>();
    public DbSet<EntityPermission> EntityPermissions => Set<EntityPermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar configuraciones desde el ensamblado
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // Filtro global para soft delete
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
    }

    /// <summary>
    /// Desactiva los filtros globales para consultas de auditoría.
    /// </summary>
    public void DisableGlobalFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasQueryFilter(e => true);
    }

    /// <summary>
    /// Reactiva los filtros globales.
    /// </summary>
    public void EnableGlobalFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
    }
}