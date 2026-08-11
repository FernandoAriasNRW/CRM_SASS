using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Docs.Application;

public static class DocsApplicationExtensions
{
    public static IServiceCollection AddDocsApplication(this IServiceCollection services)
    {
        services.AddMediatR(config => {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        return services;
    }
}
