using Automations.Application.Abstractions;
using Automations.Application.Servicios;
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
          .OrderBy(r => r.Nombre)
          .ToListAsync(ct);

  public async Task<IReadOnlyList<AutomationRule>> GetActivasPorDisparadorAsync(
      Guid tenantId, string disparador, CancellationToken ct = default)
      => await context.Rules
          .Where(r => r.TenantId == tenantId && r.Disparador == disparador && r.Activa)
          .OrderBy(r => r.Nombre)
          .ToListAsync(ct);

  public async Task<bool> ExisteConNombreAsync(
      Guid tenantId, string nombre, Guid? excepto, CancellationToken ct = default)
      => await context.Rules.AnyAsync(
          r => r.TenantId == tenantId && r.Nombre == nombre && (excepto == null || r.Id != excepto), ct);

  public async Task AddAsync(AutomationRule regla, CancellationToken ct = default)
      => await context.Rules.AddAsync(regla, ct);

  public Task UpdateAsync(AutomationRule regla, CancellationToken ct = default)
  {
    context.Rules.Update(regla);
    return Task.CompletedTask;
  }

  public Task RemoveAsync(AutomationRule regla, CancellationToken ct = default)
  {
    context.Rules.Remove(regla);
    return Task.CompletedTask;
  }
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
    services.AddScoped<IMotorDeAutomatizaciones, MotorDeAutomatizaciones>();

    return services;
  }
}
