using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Teams.Infrastructure.Persistence;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Teams.Infrastructure;

public static class TeamsInfrastructureExtensions
{
    public static IServiceCollection AddTeamsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not found.");

        services.AddDbContext<TeamsDbContext>(options =>
        {
            var provider = configuration["Database:Provider"] ?? "MySql";
            if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
            {
                options.UseMySql(connectionString, ServerVersion.Parse("8.0.32-mysql"));
            }
        });

        services.AddScoped<IUnitOfWork<TeamsDbContext>>(sp => new UnitOfWork<TeamsDbContext>(
            sp.GetRequiredService<TeamsDbContext>(),
            sp.GetRequiredService<BuildingBlocks.Infrastructure.Outbox.IOutboxService>()
        ));

        services.AddScoped<Teams.Application.Abstractions.Repositories.ITeamRepository, Teams.Infrastructure.Repositories.TeamRepository>();

        return services;
    }
}
