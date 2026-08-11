using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Tags.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTagsApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        return services;
    }
}
