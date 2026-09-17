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

    // La hora del sistema, inyectada: quien la necesite la pide en vez de leer DateTime.UtcNow,
    // y las pruebas pueden fijarla.
    services.AddSingleton(TimeProvider.System);
    services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

    // Quien resuelve las entradas del menú de navegación en todos los módulos.
    services.AddScoped<IViewScopeResolver, BuildingBlocks.Infrastructure.Views.ViewScopeResolver>();
    services.AddScoped<IOutboxService, OutboxService>();
    services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));
    services.AddHostedService<OutboxDispatcherWorker>();

    // ── Dónde se guardan los ficheros ──────────────────────────────────────────────────────
    //
    // Se elige aquí, y no dentro del servicio, porque es una decisión de despliegue: con
    // credenciales de Cloudinary se sube a Cloudinary; sin ellas, al disco del servidor.
    //
    // Antes sólo existía Cloudinary y **no está configurado en ninguna parte** —ni en desarrollo,
    // ni en producción, ni en el compose—, así que subir un fichero devolvía «Cloud name must be
    // specified in Account!» con un 400. Un servicio registrado que no puede funcionar es peor
    // que no tenerlo: parece que el sistema sabe subir ficheros.
    services.Configure<CloudinaryOptions>(configuration.GetSection("Cloudinary"));
    services.Configure<DiskStorageOptions>(configuration.GetSection(DiskStorageOptions.SectionName));

    var cloudinaryConfigured = !string.IsNullOrWhiteSpace(configuration["Cloudinary:CloudName"])
        && !string.IsNullOrWhiteSpace(configuration["Cloudinary:ApiKey"])
        && !string.IsNullOrWhiteSpace(configuration["Cloudinary:ApiSecret"]);

    if (cloudinaryConfigured)
      services.AddScoped<IStorageService, CloudinaryStorageService>();
    else
      services.AddScoped<IStorageService, DiskStorageService>();

    // MassTransit configuration
    services.AddMassTransit(x =>
    {
      // Los ensamblados se buscan por nombre, en texto, porque el módulo no se referencia desde
      // aquí. Es frágil por naturaleza: un módulo renombrado deja de aportar sus consumidores y
      // el sistema sigue arrancando como si nada, con los mensajes cayéndose sin destinatario.
      //
      // Antes el `catch` estaba vacío —«Ignore if not found»—, así que ni siquiera quedaba
      // rastro. Ahora un nombre que no carga revienta el arranque: es un error de
      // configuración, no una circunstancia, y descubrirlo semanas después por mensajes que no
      // llegan cuesta mucho más que un fallo al levantar.
      //
      // Reporting salió de la lista: sus consumidores mantenían unos modelos de lectura que
      // sólo atendían a eventos de creación —nunca de cambio de estado— y que no leía nadie.
      var assemblyNames = new[] {
          "Communication.Infrastructure",
          "Calendar.Infrastructure",
          "Ticketing.Infrastructure",
          "Notifications.Infrastructure"
      };

      foreach (var name in assemblyNames)
      {
          System.Reflection.Assembly asm;
          try
          {
              asm = System.Reflection.Assembly.Load(name);
          }
          catch (Exception ex)
          {
              throw new InvalidOperationException(
                  $"No se pudo cargar el ensamblado '{name}' para registrar sus consumidores de " +
                  "mensajes. O el nombre está mal escrito en esta lista, o el módulo dejó de " +
                  "formar parte de la solución. Sin él, sus mensajes se publican y no los " +
                  "recibe nadie.", ex);
          }

          x.AddConsumers(asm);
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
