using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Abstractions.Queries;
using Notifications.Application.Abstractions.Repositories;
using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Queries;
using Notifications.Infrastructure.Repositories;

namespace Notifications.Infrastructure;

public static class NotificationsInfrastructureExtensions
{
  public static IServiceCollection AddNotificationsInfrastructure(
      this IServiceCollection services, IConfiguration configuration)
  {
    // 1. DbContext
    services.AddDbContext<NotificationsDbContext>(options =>
        options.UseMySql(configuration.GetConnectionString("DefaultConnection"), Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.32-mysql")));

    // 2. Primero OutboxService
    services.AddScoped<IOutboxService, OutboxService>();

    // 3. UnitOfWork con OutboxService inyectado
    services.AddScoped<IUnitOfWork>(sp => new UnitOfWork<NotificationsDbContext>(
        sp.GetRequiredService<NotificationsDbContext>(),
        sp.GetRequiredService<IOutboxService>()));

    services.AddScoped<INotificationRepository, EfNotificationRepository>();
    services.AddScoped<INotificationQueries, NotificationQueries>();
    return services;
  }
}
