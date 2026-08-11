using Microsoft.EntityFrameworkCore;
using Projects.Domain.Entities;

namespace Projects.Infrastructure.Persistence;

/// <summary>
/// DbContext para el módulo Projects. Configura el filtro global de soft delete y las configuraciones de entidad.
/// </summary>
public sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : DbContext(options)
{
  public DbSet<Space> Spaces => Set<Space>();
  public DbSet<Folder> Folders => Set<Folder>();
  public DbSet<Project> Projects => Set<Project>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProjectsDbContext).Assembly);

    modelBuilder.Entity<Project>().ComplexProperty(p => p.Name);
    modelBuilder.Entity<Project>().ComplexProperty(p => p.Status);

    // Filtro global para soft delete
    modelBuilder.Entity<Project>().HasQueryFilter(e => !e.IsDeleted);
    modelBuilder.Entity<Space>().HasQueryFilter(e => !e.IsDeleted);
    modelBuilder.Entity<Folder>().HasQueryFilter(e => !e.IsDeleted);
  }

  /// <summary>
  /// Desactiva los filtros globales para consultas de auditoría.
  /// </summary>
  public void DisableGlobalFilters(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<Project>().HasQueryFilter(e => true);
    modelBuilder.Entity<Space>().HasQueryFilter(e => true);
    modelBuilder.Entity<Folder>().HasQueryFilter(e => true);
  }

  /// <summary>
  /// Reactiva los filtros globales.
  /// </summary>
  public void EnableGlobalFilters(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<Project>().HasQueryFilter(e => !e.IsDeleted);
    modelBuilder.Entity<Space>().HasQueryFilter(e => !e.IsDeleted);
    modelBuilder.Entity<Folder>().HasQueryFilter(e => !e.IsDeleted);
  }
}