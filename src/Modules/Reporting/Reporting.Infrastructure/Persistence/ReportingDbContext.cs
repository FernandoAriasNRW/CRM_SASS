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

  // Aquí había tres modelos de lectura —proyectos, tareas y tickets— alimentados por
  // consumidores de MassTransit. Se eliminaron: los consumidores sólo atendían a los eventos de
  // creación, así que una tarea se quedaba en «To Do» para siempre y un proyecto al 0 % de
  // avance, y nadie los leía. Una proyección que no se actualiza no es una caché, es una
  // trampa para quien la conecte después creyendo que está al día.
  //
  // Si en el futuro hacen falta proyecciones —para que el panel no consulte tres módulos en
  // caliente— hay que construirlas a conciencia: consumidores para todos los eventos que
  // cambian el estado, y un relleno inicial para lo que ya existe.

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);

    // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
    ApplyTenantFilters(modelBuilder);
  }

}
