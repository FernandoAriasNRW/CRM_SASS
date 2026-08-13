using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using CustomFields.Application.Abstractions;
using CustomFields.Infrastructure.Persistence;
using CustomFields.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CustomFields.Infrastructure;

/// <summary>Ata el UnitOfWork del módulo a su propio DbContext.</summary>
public sealed class CustomFieldsModuleUnitOfWork(CustomFieldsDbContext context, IOutboxService outboxService)
    : UnitOfWork<CustomFieldsDbContext>(context, outboxService), ICustomFieldsUnitOfWork
{
}

public static class CustomFieldsInfrastructureExtensions
{
  public static IServiceCollection AddCustomFieldsInfrastructure(
      this IServiceCollection services, IConfiguration configuration)
  {
    services.AddDbContext<CustomFieldsDbContext>(options =>
        options.UseMySql(configuration.GetConnectionString("DefaultConnection"),
                         ServerVersion.Parse("8.0.32-mysql")));

    services.AddScoped<IOutboxService, OutboxService>();
    services.AddScoped<ICustomFieldsUnitOfWork, CustomFieldsModuleUnitOfWork>();
    services.AddScoped<ICustomFieldRepository, EfCustomFieldRepository>();

    return services;
  }
}
