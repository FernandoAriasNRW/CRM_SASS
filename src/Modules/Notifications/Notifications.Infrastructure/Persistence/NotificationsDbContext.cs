using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

/// <summary>
/// DbContext para el módulo Notifications. Configura el filtro global de soft delete y las configuraciones de entidad.
/// </summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
  public DbSet<Notification> Notifications => Set<Notification>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // Aplicar configuraciones desde el ensamblado
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

    // Filtro global para soft delete
    modelBuilder.Entity<Notification>().HasQueryFilter(e => !e.IsDeleted);
  }

  /// <summary>
  /// Desactiva los filtros globales para consultas de auditoría.
  /// </summary>
  public void DisableGlobalFilters(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<Notification>().HasQueryFilter(e => true);
  }

  /// <summary>
  /// Reactiva los filtros globales.
  /// </summary>
  public void EnableGlobalFilters(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<Notification>().HasQueryFilter(e => !e.IsDeleted);
  }
}