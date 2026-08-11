using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Docs.Infrastructure.Persistence;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;

namespace Docs.Infrastructure;

public static class DocsInfrastructureExtensions
{
    public static IServiceCollection AddDocsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not found.");

        services.AddDbContext<DocsDbContext>(options =>
        {
            var provider = configuration["Database:Provider"] ?? "MySql";
            if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
            {
                options.UseMySql(connectionString, ServerVersion.Parse("8.0.32-mysql"));
            }
        });

        services.AddScoped<IUnitOfWork<DocsDbContext>>(sp => new UnitOfWork<DocsDbContext>(
            sp.GetRequiredService<DocsDbContext>(),
            sp.GetRequiredService<BuildingBlocks.Infrastructure.Outbox.IOutboxService>()
        ));

        services.AddScoped<Docs.Application.Abstractions.Repositories.IDocumentRepository, Repositories.DocumentRepository>();
        
        return services;
    }
}
