using BuildingBlocks.Domain;
using Ticketing.Application.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Infrastructure.Persistence;
using Ticketing.Application.Abstractions.Queries;
using Ticketing.Application.Abstractions.Repositories;
using Ticketing.Infrastructure.Queries;
using Ticketing.Infrastructure.Repositories;

namespace Ticketing.Infrastructure;

public static class TicketingInfrastructureExtensions
{
  public static IServiceCollection AddTicketingInfrastructure(
      this IServiceCollection services, IConfiguration configuration)
  {
    services.AddDbContext<TicketingDbContext>(options =>
        options.UseMySql(configuration.GetConnectionString("DefaultConnection"), Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.32-mysql")));

    // 1. Primero OutboxService
    services.AddScoped<IOutboxService, OutboxService>();

    // 2. UnitOfWork con OutboxService inyectado
    services.AddScoped<ITicketingUnitOfWork, TicketingModuleUnitOfWork>();

    services.AddScoped<ITicketRepository, EfTicketRepository>();
    services.AddScoped<ITicketQueries, TicketQueries>();
    return services;
  }
}
