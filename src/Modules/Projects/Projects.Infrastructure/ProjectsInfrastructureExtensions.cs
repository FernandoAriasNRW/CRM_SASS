using BuildingBlocks.Domain;
using Projects.Application.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Projects.Application.Abstractions.Queries;
using Projects.Application.Abstractions.Repositories;
using Projects.Infrastructure.Persistence;
using Projects.Infrastructure.Queries;
using Projects.Infrastructure.Repositories;

namespace Projects.Infrastructure;

public static class ProjectsInfrastructureExtensions
{
  public static IServiceCollection AddProjectsInfrastructure(
      this IServiceCollection services, IConfiguration configuration)
  {
    services.AddDbContext<ProjectsDbContext>(options =>
        options.UseMySql(configuration.GetConnectionString("DefaultConnection"), Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.32-mysql")));

    // 1. Primero OutboxService
    services.AddScoped<IOutboxService, OutboxService>();

    // 2. UnitOfWork con OutboxService inyectado
    services.AddScoped<IProjectsUnitOfWork, ProjectsModuleUnitOfWork>();

    services.AddScoped<IProjectRepository, EfProjectRepository>();
    services.AddScoped<ISpaceRepository, EfSpaceRepository>();
    services.AddScoped<IFolderRepository, EfFolderRepository>();
    services.AddScoped<IProjectQueries, ProjectQueries>();
    return services;
  }
}
