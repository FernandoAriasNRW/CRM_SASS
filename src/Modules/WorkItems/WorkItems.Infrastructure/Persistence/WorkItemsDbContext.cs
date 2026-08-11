using Microsoft.EntityFrameworkCore;
using WorkItems.Domain.Entities;

namespace WorkItems.Infrastructure.Persistence;

public sealed class WorkItemsDbContext(DbContextOptions<WorkItemsDbContext> options) : DbContext(options)
{
  public DbSet<WorkTask> Tasks => Set<WorkTask>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkItemsDbContext).Assembly);
    
    modelBuilder.Entity<WorkTask>().ComplexProperty(t => t.Title);
    modelBuilder.Entity<WorkTask>().ComplexProperty(t => t.Status);
  }
}