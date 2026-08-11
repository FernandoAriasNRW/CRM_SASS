using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Tags.Application.Abstractions.Repositories;
using Tags.Infrastructure.Persistence;
using Tags.Infrastructure.Repositories;

namespace Tags.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTagsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<TagsDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                b => b.MigrationsAssembly(typeof(TagsDbContext).Assembly.FullName)));

        services.AddScoped<ITagRepository, TagRepository>();

        return services;
    }
}
