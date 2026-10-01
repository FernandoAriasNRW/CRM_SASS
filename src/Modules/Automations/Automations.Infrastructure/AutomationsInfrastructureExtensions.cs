using Automations.Application.Abstractions;
using Automations.Application.Services;
using Automations.Domain.Entities;
using Automations.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Automations.Infrastructure;

/// <summary>Ata el UnitOfWork del módulo a su propio DbContext.</summary>
public sealed class AutomationsModuleUnitOfWork(AutomationsDbContext context, IOutboxService outboxService)
    : UnitOfWork<AutomationsDbContext>(context, outboxService), IAutomationsUnitOfWork
{
}

public sealed class EfAutomationRuleRepository(AutomationsDbContext context) : IAutomationRuleRepository
{
  public async Task<AutomationRule?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
      => await context.Rules
          .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

  public async Task<IReadOnlyList<AutomationRule>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
      => await context.Rules
          .Where(r => r.TenantId == tenantId)
          .OrderBy(r => r.Name)
          .ToListAsync(ct);

  public async Task<IReadOnlyList<AutomationRule>> GetActiveByTriggerAsync(
      Guid tenantId, string trigger, CancellationToken ct = default)
      => await context.Rules
          .Where(r => r.TenantId == tenantId && r.Trigger == trigger && r.IsActive)
          .OrderBy(r => r.Name)
          .ToListAsync(ct);

  public async Task<bool> ExistsWithNameAsync(
      Guid tenantId, string name, Guid? exceptId, CancellationToken ct = default)
      => await context.Rules.AnyAsync(
          r => r.TenantId == tenantId && r.Name == name && (exceptId == null || r.Id != exceptId), ct);

  public async Task AddAsync(AutomationRule rule, CancellationToken ct = default)
      => await context.Rules.AddAsync(rule, ct);

  public Task UpdateAsync(AutomationRule rule, CancellationToken ct = default)
  {
    context.Rules.Update(rule);
    return Task.CompletedTask;
  }

  public Task RemoveAsync(AutomationRule rule, CancellationToken ct = default)
  {
    context.Rules.Remove(rule);
    return Task.CompletedTask;
  }
}

public sealed class EfExecutionRepository(AutomationsDbContext context) : IExecutionRepository
{
  public async Task RecordAsync(AutomationExecution execution, CancellationToken ct = default)
      => await context.Executions.AddAsync(execution, ct);

  /// <summary>
  /// Se pregunta sólo por las que llegaron a aplicarse.
  ///
  /// Una que no cumplió condiciones no debe bloquear el aviso de mañana: si hoy la tarea no
  /// cumplía y mañana sí, el aviso tiene que salir. Contar cualquier ejecución como «ya hecha»
  /// silenciaría precisamente el día en que la regla empieza a tener razón.
  /// </summary>
  public Task<bool> AlreadyRanTodayAsync(
      Guid tenantId, Guid ruleId, Guid entityId, DateOnly day, CancellationToken ct = default)
      => context.Executions.AnyAsync(
          x => x.TenantId == tenantId
            && x.RuleId == ruleId
            && x.EntityId == entityId
            && x.Day == day
            && x.Outcome == ExecutionOutcomes.Applied, ct);

  public async Task<IReadOnlyList<AutomationExecution>> LatestForRuleAsync(
      Guid tenantId, Guid ruleId, int count, CancellationToken ct = default)
      => await context.Executions
          .AsNoTracking()
          .Where(x => x.TenantId == tenantId && x.RuleId == ruleId)
          .OrderByDescending(x => x.AtUtc)
          .Take(count)
          .ToListAsync(ct);
}

public static class AutomationsInfrastructureExtensions
{
  public static IServiceCollection AddAutomationsInfrastructure(
      this IServiceCollection services, IConfiguration configuration)
  {
    services.AddDbContext<AutomationsDbContext>(options =>
        options.UseMySql(configuration.GetConnectionString("DefaultConnection"),
                         ServerVersion.Parse("8.0.32-mysql")));

    services.AddScoped<IOutboxService, OutboxService>();
    services.AddScoped<IAutomationsUnitOfWork, AutomationsModuleUnitOfWork>();
    services.AddScoped<IAutomationRuleRepository, EfAutomationRuleRepository>();
    services.AddScoped<IExecutionRepository, EfExecutionRepository>();
    services.AddScoped<IAutomationEngine, AutomationEngine>();

    return services;
  }
}
