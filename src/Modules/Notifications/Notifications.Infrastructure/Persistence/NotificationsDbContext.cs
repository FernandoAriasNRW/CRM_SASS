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

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // Aplicar configuraciones desde el ensamblado
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

    // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
    ApplyTenantFilters(modelBuilder);
  }

}
