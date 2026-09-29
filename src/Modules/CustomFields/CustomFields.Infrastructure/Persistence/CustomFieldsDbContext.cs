using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using CustomFields.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CustomFields.Infrastructure.Persistence;

public sealed class CustomFieldsDbContext(DbContextOptions<CustomFieldsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
  public DbSet<CustomFieldDefinition> Definitions => Set<CustomFieldDefinition>();
  public DbSet<CustomFieldValue> Values => Set<CustomFieldValue>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<CustomFieldDefinition>(e =>
    {
      e.ToTable("CustomFieldDefinitions");
      e.Property(d => d.Name).HasMaxLength(CustomFieldDefinition.MaxNameLength).IsRequired();
      e.Property(d => d.Type).HasMaxLength(30).IsRequired();
      e.Property(d => d.TargetEntity).HasMaxLength(30).IsRequired();

      // Las opciones son una lista corta y cerrada que sólo se lee entera: una tabla aparte
      // sería un join por cada formulario para no ganar nada.
      e.PrimitiveCollection(d => d.Options);

      // El nombre es lo que ve la gente: dos campos «Cliente» en la misma entidad serían
      // indistinguibles. Lo garantiza la base, no sólo el handler, porque dos peticiones
      // simultáneas pasarían las dos la comprobación previa.
      e.HasIndex(d => new { d.TenantId, d.TargetEntity, d.Name })
       .IsUnique()
       .HasDatabaseName("UX_CustomFieldDefinitions_Tenant_TargetEntity_Name");
    });

    modelBuilder.Entity<CustomFieldValue>(e =>
    {
      e.ToTable("CustomFieldValues");

      // El texto canónico puede ser largo en una selección múltiple, pero no ilimitado: 4000
      // caracteres dan de sobra y permiten indexar por prefijo si algún día hace falta.
      e.Property(v => v.Value).HasMaxLength(4000);

      // Un valor por campo y entidad. Sin esto, guardar dos veces el mismo campo dejaría dos
      // filas y la que se leyera dependería del orden de la consulta.
      e.HasIndex(v => new { v.TenantId, v.DefinitionId, v.EntityId })
       .IsUnique()
       .HasDatabaseName("UX_CustomFieldValues_Tenant_Definition_Entity");

      // Por aquí se piden los valores de una entidad al abrir su detalle.
      e.HasIndex(v => new { v.TenantId, v.EntityId })
       .HasDatabaseName("IX_CustomFieldValues_Tenant_Entity");
    });

    ApplyTenantFilters(modelBuilder);
  }
}
