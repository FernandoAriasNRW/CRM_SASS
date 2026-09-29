using Automations.Domain.Entities;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Automations.Infrastructure.Persistence;

public sealed class AutomationsDbContext(DbContextOptions<AutomationsDbContext> options, IUserContext? userContext)
    : TenantDbContext(options, userContext)
{
  public DbSet<AutomationRule> Rules => Set<AutomationRule>();
  public DbSet<AutomationExecution> Executions => Set<AutomationExecution>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<AutomationExecution>(e =>
    {
      e.ToTable("AutomationExecutions");
      e.Property(x => x.Outcome).HasMaxLength(30).IsRequired();
      e.Property(x => x.Detail).HasMaxLength(AutomationExecution.MaxDetailLength);

      // La consulta de la memoria diaria: «¿esta regla ya se aplicó hoy sobre esta tarea?». La
      // hace el trabajo de vencimientos una vez por regla y por tarea, así que sin índice sería
      // un recorrido de la tabla entera multiplicado por el número de tareas del inquilino —y
      // esta tabla sólo crece.
      e.HasIndex(x => new { x.TenantId, x.RuleId, x.EntityId, x.Day })
       .HasDatabaseName("IX_AutomationExecutions_DailyMemory");

      // La otra consulta: el historial de una regla, de lo más reciente a lo más antiguo.
      e.HasIndex(x => new { x.TenantId, x.RuleId, x.AtUtc })
       .HasDatabaseName("IX_AutomationExecutions_History");
    });

    modelBuilder.Entity<AutomationRule>(e =>
    {
      e.ToTable("AutomationRules");
      e.Property(r => r.Name).HasMaxLength(AutomationRule.MaxNameLength).IsRequired();
      e.Property(r => r.Trigger).HasMaxLength(50).IsRequired();

      // El nombre es lo único que distingue una automatización de otra en la lista. Lo garantiza
      // la base y no sólo el handler, porque dos peticiones simultáneas pasarían las dos la
      // comprobación previa.
      e.HasIndex(r => new { r.TenantId, r.Name })
       .IsUnique()
       .HasDatabaseName("UX_AutomationRules_Tenant_Name");

      // Es la consulta del motor, y ocurre en cada evento de tarea del inquilino.
      e.HasIndex(r => new { r.TenantId, r.Trigger, r.IsActive })
       .HasDatabaseName("IX_AutomationRules_Tenant_Trigger_IsActive");

      // Condiciones y acciones son listas cortas que sólo se leen enteras con su regla: tablas
      // aparte serían dos joins por evento para no ganar nada.
      e.OwnsMany(r => r.Conditions, c =>
      {
        c.ToTable("AutomationConditions");
        c.WithOwner().HasForeignKey("AutomationRuleId");
        c.HasKey("AutomationRuleId", nameof(AutomationCondition.Id));
        c.Property(x => x.Field).HasMaxLength(50).IsRequired();
        c.Property(x => x.Operator).HasMaxLength(30).IsRequired();
        c.Property(x => x.Value).HasMaxLength(500);
      });

      e.OwnsMany(r => r.Actions, a =>
      {
        a.ToTable("AutomationActions");
        a.WithOwner().HasForeignKey("AutomationRuleId");
        a.HasKey("AutomationRuleId", nameof(AutomationAction.Id));
        a.Property(x => x.Type).HasMaxLength(50).IsRequired();
        a.Property(x => x.Value).HasMaxLength(500).IsRequired();
      });
    });

    ApplyTenantFilters(modelBuilder);
  }
}
