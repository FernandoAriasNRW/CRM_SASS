using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.Persistence
{
  public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
  {
    public CrmDbContext CreateDbContext(string[] args)
    {
      var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../../Host/ApiHost");

      // 👇 Cargar configuración manualmente
      var configuration = new ConfigurationBuilder()
          .SetBasePath(basePath)
          .AddJsonFile("appsettings.json", optional: false)
          .AddJsonFile("appsettings.Development.json", optional: true)
          .AddEnvironmentVariables()
          .Build();

      // 👇 Usar tu extensión existente
      var services = new ServiceCollection();
      services.AddDatabase(configuration);

      var provider = services.BuildServiceProvider();

      return provider.GetRequiredService<CrmDbContext>();
    }
  }
}