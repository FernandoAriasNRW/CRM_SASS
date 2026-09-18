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

    /// <summary>Dónde está pegado cada comentario en línea. Ver <see cref="DocumentAnnotation"/>.</summary>
    public DbSet<DocumentAnnotation> DocumentAnnotations => Set<DocumentAnnotation>();

    /// <summary>Cuánto se usa cada plantilla. Ver <see cref="TemplateUsage"/>.</summary>
    public DbSet<TemplateUsage> TemplateUsages => Set<TemplateUsage>();

    /// <summary>Lo que cada página menciona. Ver <see cref="Domain.Mentions.DocumentMention"/>.</summary>
    public DbSet<Domain.Mentions.DocumentMention> DocumentMentions
        => Set<Domain.Mentions.DocumentMention>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocsDbContext).Assembly);

        // Soft delete query filters

      // Aislamiento por tenant y soft delete, compuestos en un solo filtro.
      // Las dos preguntas que se hacen sobre esta tabla, cada una con su índice: «qué menciona
      // esta página» —al reescribir tras guardar— y «quién menciona esta tarea» —al abrir su
      // pantalla—. Sin el segundo, cada apertura de una tarea recorre la tabla entera.
      modelBuilder.Entity<Domain.Mentions.DocumentMention>()
          .HasIndex(m => new { m.TenantId, m.PageId })
          .HasDatabaseName("IX_DocumentMentions_TenantId_PageId");

      modelBuilder.Entity<Domain.Mentions.DocumentMention>()
          .HasIndex(m => new { m.TenantId, m.MentionedType, m.MentionedEntityId })
          .HasDatabaseName("IX_DocumentMentions_TenantId_MentionedType_MentionedEntityId");

      // Una fila por plantilla y por inquilino. La unicidad va en la base y no sólo en el
      // código porque dos personas creando a la vez desde la misma plantilla harían dos filas,
      // y a partir de ahí el contador se reparte entre ambas y nunca sube.
      modelBuilder.Entity<TemplateUsage>()
          .HasIndex(u => new { u.TenantId, u.Key })
          .IsUnique()
          .HasDatabaseName("IX_TemplateUsages_TenantId_Key");

      modelBuilder.Entity<TemplateUsage>()
          .Property(u => u.Key)
          .HasMaxLength(100)
          .IsRequired();

      // La pregunta que se hace sobre esta tabla es siempre la misma —«qué se ha comentado en
      // esta página»— y se hace al abrir cada página, así que sin este índice cada apertura
      // recorre las anotaciones del inquilino entero.
      modelBuilder.Entity<DocumentAnnotation>()
          .HasIndex(a => new { a.TenantId, a.PageId })
          .HasDatabaseName("IX_DocumentAnnotations_TenantId_PageId");

      modelBuilder.Entity<DocumentAnnotation>()
          .Property(a => a.QuotedText)
          .HasMaxLength(DocumentAnnotation.MaxQuoteLength)
          .IsRequired();

      ApplyTenantFilters(modelBuilder);
    }
}
