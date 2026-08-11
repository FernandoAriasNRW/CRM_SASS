using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tags.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddTagsPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
