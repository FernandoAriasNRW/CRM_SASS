using Ticketing.Presentation.Endpoints;

namespace ApiHost.Startup;

public static class CorsSetup
{
    /// <summary>La política del resto de la API: sólo los orígenes configurados, con credenciales.</summary>
    public const string DefaultPolicy = "AllowSpecificOrigins";

    public static IServiceCollection AddCorsPolicies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

            // La entrada de tickets se llama desde la web de cada cliente, en un dominio que la
            // aplicación no conoce. Sin credenciales —no hay cookies ni sesión que proteger— y sólo con
            // lo que ese endpoint necesita. El resto de la API sigue con su lista de orígenes.
            options.AddPolicy(TicketingEndpoints.IntakeCorsPolicy, policy =>
            {
                policy.AllowAnyOrigin()
                      .WithMethods("POST")
                      .WithHeaders("Content-Type", TicketingEndpoints.ApiKeyHeader);
            });

            options.AddPolicy(DefaultPolicy, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
