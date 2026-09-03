using Automations.Domain.Entities;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Automations.Infrastructure.Persistence;

public sealed class AutomationsDbContext(DbContextOptions<AutomationsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
  public DbSet<AutomationRule> Rules => Set<AutomationRule>();
  public DbSet<EjecucionDeAutomatizacion> Ejecuciones => Set<EjecucionDeAutomatizacion>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<EjecucionDeAutomatizacion>(e =>
    {
      e.ToTable("AutomationExecutions");
      e.Property(x => x.Resultado).HasMaxLength(30).IsRequired();
      e.Property(x => x.Detalle).HasMaxLength(EjecucionDeAutomatizacion.LargoMaximoDelDetalle);

      // La consulta de la memoria diaria: «¿esta regla ya se aplicó hoy sobre esta tarea?». La
      // hace el trabajo de vencimientos una vez por regla y por tarea, así que sin índice sería
      // un recorrido de la tabla entera multiplicado por el número de tareas del inquilino —y
      // esta tabla sólo crece.
      e.HasIndex(x => new { x.TenantId, x.RuleId, x.EntityId, x.Dia })
       .HasDatabaseName("IX_AutomationExecutions_Memoria");

      // La otra consulta: el historial de una regla, de lo más reciente a lo más antiguo.
      e.HasIndex(x => new { x.TenantId, x.RuleId, x.CuandoUtc })
       .HasDatabaseName("IX_AutomationExecutions_Historial");
    });

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
