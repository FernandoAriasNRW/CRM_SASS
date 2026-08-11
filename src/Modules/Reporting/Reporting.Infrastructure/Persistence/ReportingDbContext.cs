using Microsoft.EntityFrameworkCore;
using Reporting.Domain.Entities;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// DbContext para el módulo Reporting. Configura el filtro global de soft delete y las configuraciones de entidad.
/// </summary>
public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
  public DbSet<Report> Reports => Set<Report>();
  public DbSet<Dashboard> Dashboards => Set<Dashboard>();
  public DbSet<ProjectReadModel> Projects => Set<ProjectReadModel>();
  public DbSet<TaskReadModel> Tasks => Set<TaskReadModel>();
  public DbSet<TicketReadModel> Tickets => Set<TicketReadModel>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);

    // Filtro global para soft delete
    modelBuilder.Entity<Report>().HasQueryFilter(e => !e.IsDeleted);
    modelBuilder.Entity<ProjectReadModel>().HasQueryFilter(e => !e.IsDeleted);
  }

  /// <summary>
  /// Desactiva los filtros globales para consultas de auditoría.
  /// </summary>
  public void DisableGlobalFilters(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<Report>().HasQueryFilter(e => true);
    modelBuilder.Entity<ProjectReadModel>().HasQueryFilter(e => true);
  }

  /// <summary>
  /// Reactiva los filtros globales.
  /// </summary>
  public void EnableGlobalFilters(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<Report>().HasQueryFilter(e => !e.IsDeleted);
    modelBuilder.Entity<ProjectReadModel>().HasQueryFilter(e => !e.IsDeleted);
  }
}
