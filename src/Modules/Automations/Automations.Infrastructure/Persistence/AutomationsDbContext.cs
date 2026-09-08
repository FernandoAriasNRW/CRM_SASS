using Automations.Domain.Entities;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Automations.Infrastructure.Persistence;

public sealed class AutomationsDbContext(DbContextOptions<AutomationsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
  public DbSet<AutomationRule> Rules => Set<AutomationRule>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<AutomationRule>(e =>
    {
      e.ToTable("AutomationRules");
      e.Property(r => r.Nombre).HasMaxLength(AutomationRule.LargoMaximoDelNombre).IsRequired();
      e.Property(r => r.Disparador).HasMaxLength(50).IsRequired();

      // El nombre es lo único que distingue una automatización de otra en la lista. Lo garantiza
      // la base y no sólo el handler, porque dos peticiones simultáneas pasarían las dos la
      // comprobación previa.
      e.HasIndex(r => new { r.TenantId, r.Nombre })
       .IsUnique()
       .HasDatabaseName("UX_AutomationRules_Tenant_Nombre");

      // Es la consulta del motor, y ocurre en cada evento de tarea del inquilino.
      e.HasIndex(r => new { r.TenantId, r.Disparador, r.Activa })
       .HasDatabaseName("IX_AutomationRules_Tenant_Disparador_Activa");

      // Condiciones y acciones son listas cortas que sólo se leen enteras con su regla: tablas
      // aparte serían dos joins por evento para no ganar nada.
      e.OwnsMany(r => r.Condiciones, c =>
      {
        c.ToTable("AutomationConditions");
        c.WithOwner().HasForeignKey("AutomationRuleId");
        c.HasKey("AutomationRuleId", nameof(CondicionDeAutomatizacion.Id));
        c.Property(x => x.Campo).HasMaxLength(50).IsRequired();
        c.Property(x => x.Operador).HasMaxLength(30).IsRequired();
        c.Property(x => x.Valor).HasMaxLength(500);
      });

      e.OwnsMany(r => r.Acciones, a =>
      {
        a.ToTable("AutomationActions");
        a.WithOwner().HasForeignKey("AutomationRuleId");
        a.HasKey("AutomationRuleId", nameof(AccionDeAutomatizacion.Id));
        a.Property(x => x.Tipo).HasMaxLength(50).IsRequired();
        a.Property(x => x.Valor).HasMaxLength(500).IsRequired();
      });
    });

    ApplyTenantFilters(modelBuilder);
  }
}
