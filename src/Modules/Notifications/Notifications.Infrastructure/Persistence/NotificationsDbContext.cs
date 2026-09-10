using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

/// <summary>
/// DbContext del modulo Notifications. Hereda de TenantDbContext: el aislamiento por
/// tenant y el soft delete se aplican solos a toda entidad marcada.
/// </summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
  public DbSet<Notification> Notifications => Set<Notification>();
  public DbSet<PreferenciasDeNotificacion> PreferenciasDeNotificacion => Set<PreferenciasDeNotificacion>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // Aplicar configuraciones desde el ensamblado
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

    modelBuilder.Entity<PreferenciasDeNotificacion>(p =>
    {
      p.ToTable("NotificationPreferences");

      // Una sola fila por persona y organización. La restricción va en la base y no sólo en el
      // código porque dos pestañas guardando a la vez pueden intentar crear dos filas, y
      // entonces cuál de las dos se lee al día siguiente es cuestión de suerte.
      p.HasIndex(x => new { x.TenantId, x.UserId })
       .IsUnique()
       .HasDatabaseName("UX_NotificationPreferences_Tenant_User");

      // TimeOnly va a una columna `time`, no a texto: comparar horas es lo único que se hace
      // con esto, y sobre texto la comparación sería alfabética.
      p.Property(x => x.QuietHoursStart).HasColumnType("time");
      p.Property(x => x.QuietHoursEnd).HasColumnType("time");
    });

    // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
    ApplyTenantFilters(modelBuilder);
  }

}
