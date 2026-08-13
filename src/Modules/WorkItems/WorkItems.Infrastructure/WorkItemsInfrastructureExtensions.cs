using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkItems.Application.Abstractions.Queries;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Infrastructure.Persistence;
using WorkItems.Infrastructure.Queries;
using WorkItems.Infrastructure.Repositories;

namespace WorkItems.Infrastructure;

public static class WorkItemsInfrastructureExtensions
{
  public static IServiceCollection AddWorkItemsInfrastructure(
      this IServiceCollection services, IConfiguration configuration)
  {
    services.AddDbContext<WorkItemsDbContext>(options =>
        options.UseMySql(configuration.GetConnectionString("DefaultConnection"), Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.32-mysql")));

    // 1. Primero OutboxService
    services.AddScoped<IOutboxService, OutboxService>();

    // 2. UnitOfWork con OutboxService inyectado
    services.AddScoped<IWorkItemsUnitOfWork, WorkItemsModuleUnitOfWork>();

    services.AddScoped<ITaskRepository, EfTaskRepository>();
    services.AddScoped<ITaskDependencyRepository, EfTaskDependencyRepository>();
    services.AddScoped<ITaskQueries, TaskQueries>();

    services.AddScoped<Recurrencia.GeneradorDeTareasRecurrentes>();
    services.AddHostedService<Recurrencia.RecurringTasksWorker>();

    return services;
  }
}
