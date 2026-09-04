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

  /// <summary>Las peticiones de exportar un informe a fichero, con su estado.</summary>
  public DbSet<Exportacion> Exportaciones => Set<Exportacion>();

  /// <summary>Los bytes, en su propia tabla para que listar exportaciones no los arrastre.</summary>
  public DbSet<ContenidoDeExportacion> ContenidosDeExportacion => Set<ContenidoDeExportacion>();

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

    // El trabajador de exportaciones busca lo pendiente por estado y por antigüedad, cada pocos
    // segundos y sobre todos los inquilinos. Sin índice, ese sondeo recorre la tabla entera cada
    // vez, y la tabla sólo crece.
    modelBuilder.Entity<Exportacion>()
        .HasIndex(e => new { e.EstadoValue, e.SolicitadaUtc })
        .HasDatabaseName("IX_Exportaciones_Estado_Solicitada");

    modelBuilder.Entity<Exportacion>()
        .HasIndex(e => new { e.TenantId, e.ReportId })
        .HasDatabaseName("IX_Exportaciones_TenantId_ReportId");

    // Un contenido por exportación. El índice único lo hace cumplir la base: si dos reintentos
    // llegaran a guardar los dos, la descarga tendría dos ficheros candidatos y elegiría uno
    // por orden de lectura, que es como se sirve el fichero equivocado.
    modelBuilder.Entity<ContenidoDeExportacion>()
        .HasIndex(c => c.ExportacionId)
        .IsUnique()
        .HasDatabaseName("UX_ContenidosDeExportacion_ExportacionId");

    // Los bytes van a LONGBLOB: el tipo por defecto de un byte[] en MySQL es BLOB, que corta a
    // 64 KB **sin avisar**. Un informe de mil filas lo pasa, y el fichero llegaría truncado.
    modelBuilder.Entity<ContenidoDeExportacion>()
        .Property(c => c.Bytes)
        .HasColumnType("LONGBLOB");

    // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
    ApplyTenantFilters(modelBuilder);
  }

}
