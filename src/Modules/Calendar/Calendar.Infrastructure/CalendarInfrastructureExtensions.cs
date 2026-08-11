using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Calendar.Application.Abstractions.Queries;
using Calendar.Application.Abstractions.Repositories;
using Calendar.Infrastructure.Persistence;
using Calendar.Infrastructure.Queries;
using Calendar.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Calendar.Infrastructure;

/// <summary>
/// Extensiones para registrar los servicios de infraestructura del módulo Calendar.
/// </summary>
public static class CalendarInfrastructureExtensions
{
  /// <summary>
  /// Registra todos los servicios de infraestructura del módulo Calendar.
  /// </summary>
  public static IServiceCollection AddCalendarInfrastructure(
      this IServiceCollection services, IConfiguration configuration)
  {
    // 1. DbContext
    services.AddDbContext<CalendarDbContext>(options =>
        options.UseMySql(configuration.GetConnectionString("DefaultConnection"), Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.32-mysql")));

    // 2. Primero OutboxService
    services.AddScoped<IOutboxService, OutboxService>();

    // 3. UnitOfWork con OutboxService inyectado
    services.AddScoped<IUnitOfWork>(sp => new UnitOfWork<CalendarDbContext>(
        sp.GetRequiredService<CalendarDbContext>(),
        sp.GetRequiredService<IOutboxService>()));

    // Repository para escritura (Commands)
    services.AddScoped<ICalendarEventRepository, EfCalendarEventRepository>();

    // Query Services para lectura (Queries) - Separación CQRS
    services.AddScoped<ICalendarEventQueries, CalendarEventQueries>();

    return services;
  }
}
