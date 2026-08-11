using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Persistence;

public static class DatabaseExtensions
{
  /// <summary>
  /// Registra CrmDbContext con el proveedor definido en Database:Provider. Valores válidos: PostgreSql (default) |
  /// SqlServer | MySql | Sqlite /// Para cambiar de motor:
  /// 1. Cambiar "Database:Provider" en appsettings.json
  /// 2. Asegurarse de que la connection string correspondiente existe
  /// 3. Si es la primera vez con ese motor: generar migraciones (ver README) /// Cada proveedor tiene su propia carpeta
  /// de migraciones: Persistence/Migrations/PostgreSql/ Persistence/Migrations/SqlServer/ Persistence/Migrations/MySql/
  /// </summary>
  public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
  {
    var provider = config["Database:Provider"] ?? "MySql";
    var connectionString = config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            $"Connection string 'DefaultConnection' not found. " +
            $"Add 'ConnectionStrings:DefaultConnection' to appsettings.json.");

    services.AddDbContext<CrmDbContext>(options =>
    {
      ConfigureProvider(options, provider, connectionString);
      options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);
    });

    return services;
  }

  private static void ConfigureProvider(DbContextOptionsBuilder options, string provider, string connectionString)
  {
    switch (provider.ToLowerInvariant())
    {
      case "mysql":
        options.UseMySql(connectionString, ServerVersion.Parse("8.0.32-mysql"), mysql =>
        {
          mysql.MigrationsAssembly("BuildingBlocks.Infrastructure");
          mysql.MigrationsHistoryTable("__ef_migrations_history");
          mysql.EnableRetryOnFailure(3);
        });
        break;

      default:
        throw new NotSupportedException(
            $"Provider '{provider}' not supported. Valid: PostgreSql, SqlServer, MySql, Sqlite");
    }
  }
}