using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.DomainEvents;
using BuildingBlocks.Infrastructure.Email;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using BuildingBlocks.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;

public static class InfrastructureExtensions
{
  public static IServiceCollection AddCoreInfrastructure(
      this IServiceCollection services,
      IConfiguration configuration)
  {
    // 1. Servicios Transversales (Singleton/Scoped que no dependen del DBContext)
    services.AddScoped<IEmailService, SmtpEmailService>();
    services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
    services.AddScoped<IOutboxService, OutboxService>();
    services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));
    services.AddHostedService<OutboxDispatcherWorker>();

    // Storage
    services.Configure<CloudinaryOptions>(configuration.GetSection("Cloudinary"));
    services.AddScoped<IStorageService, CloudinaryStorageService>();

    // MassTransit configuration
    services.AddMassTransit(x =>
    {
      var assemblyNames = new[] { 
          "Reporting.Infrastructure",
          "Communication.Infrastructure", 
          "Calendar.Infrastructure", 
          "Ticketing.Infrastructure", 
          "Notifications.Infrastructure" 
      };

      foreach (var name in assemblyNames)
      {
          try 
          {
              var asm = System.Reflection.Assembly.Load(name);
              x.AddConsumers(asm);
          } 
          catch 
          { 
              // Ignore if not found
          }
      }

      x.UsingRabbitMq((context, cfg) =>
      {
        var rabbitHost = configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitUser = configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitPass = configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(rabbitHost, "/", h =>
        {
          h.Username(rabbitUser);
          h.Password(rabbitPass);
        });

        cfg.ConfigureEndpoints(context);
      });
    });

    // 2. Registro de Webhooks (usa HttpClient interno)
    //services.AddWebhookServices(configuration);

    return services;
  }

  // M�todo para que cada m�dulo registre su persistencia de forma aislada
  public static IServiceCollection AddModulePersistence<TContext>(
      this IServiceCollection services,
      IConfiguration configuration) where TContext : DbContext
  {
    var connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string not found.");

    services.AddDbContext<TContext>(options =>
    {
      var provider = configuration["Database:Provider"] ?? "MySql";
      options.UseDatabaseWithProvider(provider, connectionString);
    });

    // 1. Primero registrar OutboxService
    services.AddScoped<IOutboxService, OutboxService>();

    // 2. Luego registrar UnitOfWork que depende de IOutboxService
    services.AddScoped<IUnitOfWork<TContext>, UnitOfWork<TContext>>();

    return services;
  }

  private static void UseDatabaseWithProvider(
      this DbContextOptionsBuilder builder,
      string provider,
      string connectionString)
  {
    if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
    {
      builder.UseMySql(connectionString, ServerVersion.Parse("8.0.32-mysql"));
    }
    // Agregar otros proveedores aqu�
  }
}
