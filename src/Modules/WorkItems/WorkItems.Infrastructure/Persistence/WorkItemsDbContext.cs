using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WorkItems.Domain.Entities;

namespace WorkItems.Infrastructure.Persistence;

public sealed class WorkItemsDbContext(DbContextOptions<WorkItemsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
  public DbSet<WorkTask> Tasks => Set<WorkTask>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkItemsDbContext).Assembly);
    
    modelBuilder.Entity<WorkTask>().ComplexProperty(t => t.Title);
    modelBuilder.Entity<WorkTask>().ComplexProperty(t => t.Status);

    // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
    ApplyTenantFilters(modelBuilder);
  }
}
