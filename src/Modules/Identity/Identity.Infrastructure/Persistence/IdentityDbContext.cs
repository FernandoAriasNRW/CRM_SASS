using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// DbContext para el módulo Identity.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
    public DbSet<User> User => Set<User>();
    public DbSet<SavedView> SavedViews => Set<SavedView>();
    public DbSet<EntityPermission> EntityPermissions => Set<EntityPermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar configuraciones desde el ensamblado
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

      // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
      ApplyTenantFilters(modelBuilder);
    }

}
