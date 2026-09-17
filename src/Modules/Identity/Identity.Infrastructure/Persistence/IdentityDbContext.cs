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
    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar configuraciones desde el ensamblado
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        modelBuilder.Entity<Favorite>(f =>
        {
            f.ToTable("Favorites");
            f.Property(x => x.EntityType).HasMaxLength(30).IsRequired();

            // Una persona no puede marcar dos veces lo mismo. La restricción va en la base y no
            // sólo en el manejador porque dos pulsaciones seguidas de la estrella pasarían las
            // dos la comprobación previa y dejarían dos filas — y entonces desmarcar borraría
            // una y la estrella seguiría encendida.
            f.HasIndex(x => new { x.TenantId, x.UserId, x.EntityType, x.EntityId })
             .IsUnique()
             .HasDatabaseName("UX_Favorites_User_Entity");
        });

      // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
      ApplyTenantFilters(modelBuilder);
    }

}
