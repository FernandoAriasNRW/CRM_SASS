using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Docs.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Docs.Infrastructure.Persistence;

public sealed class DocsDbContext(DbContextOptions<DocsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<DocumentPermission> DocumentPermissions => Set<DocumentPermission>();

    /// <summary>Dónde está pegado cada comentario en línea. Ver <see cref="AnotacionEnDocumento"/>.</summary>
    public DbSet<AnotacionEnDocumento> AnotacionesEnDocumentos => Set<AnotacionEnDocumento>();

    /// <summary>Cuánto se usa cada plantilla. Ver <see cref="UsoDePlantilla"/>.</summary>
    public DbSet<UsoDePlantilla> UsosDePlantilla => Set<UsoDePlantilla>();

    /// <summary>Lo que cada página menciona. Ver <see cref="Domain.Menciones.MencionEnDocumento"/>.</summary>
    public DbSet<Domain.Menciones.MencionEnDocumento> MencionesEnDocumentos
        => Set<Domain.Menciones.MencionEnDocumento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocsDbContext).Assembly);

        // Soft delete query filters

      // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
      // Las dos preguntas que se hacen sobre esta tabla, cada una con su índice: «qué menciona
      // esta página» —al reescribir tras guardar— y «quién menciona esta tarea» —al abrir su
      // pantalla—. Sin el segundo, cada apertura de una tarea recorre la tabla entera.
      modelBuilder.Entity<Domain.Menciones.MencionEnDocumento>()
          .HasIndex(m => new { m.TenantId, m.PageId })
          .HasDatabaseName("IX_Menciones_TenantId_PageId");

      modelBuilder.Entity<Domain.Menciones.MencionEnDocumento>()
          .HasIndex(m => new { m.TenantId, m.TipoMencionado, m.EntidadMencionadaId })
          .HasDatabaseName("IX_Menciones_TenantId_Tipo_Entidad");

      // Una fila por plantilla y por inquilino. La unicidad va en la base y no sólo en el
      // código porque dos personas creando a la vez desde la misma plantilla harían dos filas,
      // y a partir de ahí el contador se reparte entre ambas y nunca sube.
      modelBuilder.Entity<UsoDePlantilla>()
          .HasIndex(u => new { u.TenantId, u.Clave })
          .IsUnique()
          .HasDatabaseName("IX_UsosDePlantilla_TenantId_Clave");

      modelBuilder.Entity<UsoDePlantilla>()
          .Property(u => u.Clave)
          .HasMaxLength(100)
          .IsRequired();

      // La pregunta que se hace sobre esta tabla es siempre la misma —«qué se ha comentado en
      // esta página»— y se hace al abrir cada página, así que sin este índice cada apertura
      // recorre las anotaciones del inquilino entero.
      modelBuilder.Entity<AnotacionEnDocumento>()
          .HasIndex(a => new { a.TenantId, a.PageId })
          .HasDatabaseName("IX_Anotaciones_TenantId_PageId");

      modelBuilder.Entity<AnotacionEnDocumento>()
          .Property(a => a.TextoCitado)
          .HasMaxLength(AnotacionEnDocumento.LargoDeLaCita)
          .IsRequired();

      ApplyTenantFilters(modelBuilder);
    }
}
