using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Projects.Domain.Entities;

namespace Projects.Infrastructure.Persistence;

/// <summary>
/// DbContext del modulo Projects. Hereda de TenantDbContext: el aislamiento por
/// tenant y el soft delete se aplican solos a toda entidad marcada.
/// </summary>
public sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
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

    // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
    ApplyTenantFilters(modelBuilder);
  }

}
