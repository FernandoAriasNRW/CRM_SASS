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
      e.Property(d => d.Nombre).HasMaxLength(CustomFieldDefinition.LargoMaximoDelNombre).IsRequired();
      e.Property(d => d.Tipo).HasMaxLength(30).IsRequired();
      e.Property(d => d.EntidadDestino).HasMaxLength(30).IsRequired();

      // Las opciones son una lista corta y cerrada que sólo se lee entera: una tabla aparte
      // sería un join por cada formulario para no ganar nada.
      e.PrimitiveCollection(d => d.Opciones);

      // El nombre es lo que ve la gente: dos campos «Cliente» en la misma entidad serían
      // indistinguibles. Lo garantiza la base, no sólo el handler, porque dos peticiones
      // simultáneas pasarían las dos la comprobación previa.
      e.HasIndex(d => new { d.TenantId, d.EntidadDestino, d.Nombre })
       .IsUnique()
       .HasDatabaseName("UX_CustomFieldDefinitions_Tenant_Entidad_Nombre");
    });

    modelBuilder.Entity<CustomFieldValue>(e =>
    {
      e.ToTable("CustomFieldValues");

      // El texto canónico puede ser largo en una selección múltiple, pero no ilimitado: 4000
      // caracteres dan de sobra y permiten indexar por prefijo si algún día hace falta.
      e.Property(v => v.Valor).HasMaxLength(4000);

      // Un valor por campo y entidad. Sin esto, guardar dos veces el mismo campo dejaría dos
      // filas y la que se leyera dependería del orden de la consulta.
      e.HasIndex(v => new { v.TenantId, v.DefinitionId, v.EntityId })
       .IsUnique()
       .HasDatabaseName("UX_CustomFieldValues_Tenant_Definicion_Entidad");

      // Por aquí se piden los valores de una entidad al abrir su detalle.
      e.HasIndex(v => new { v.TenantId, v.EntityId })
       .HasDatabaseName("IX_CustomFieldValues_Tenant_Entidad");
    });

    ApplyTenantFilters(modelBuilder);
  }
}
