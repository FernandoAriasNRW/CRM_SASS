using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Reporting.Domain.Entities;

namespace Reporting.Infrastructure.Persistence;

/// <summary>
/// DbContext del modulo Reporting. Hereda de TenantDbContext: el aislamiento por
/// tenant y el soft delete se aplican solos a toda entidad marcada.
/// </summary>
public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
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

    // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
    ApplyTenantFilters(modelBuilder);
  }

}
