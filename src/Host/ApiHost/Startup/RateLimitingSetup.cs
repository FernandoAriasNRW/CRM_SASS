using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Ticketing.Domain.Entities;
using Ticketing.Presentation.Endpoints;

namespace ApiHost.Startup;

/// <summary>
/// Rate limiting. La política global protege toda la API; las políticas nombradas blindan los
/// puntos abusables sin autenticación previa: la entrada de tickets y el login (fuerza bruta de
/// credenciales).
/// </summary>
public static class RateLimitingSetup
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // El límite global sale de configuración para poder subirlo en las pruebas de integración.
        // Todas se autentican como el mismo administrador, así que comparten partición y la suite
        // entera cabe en una sola ventana: al crecer, empezaron a salir 429 según el orden de
        // ejecución, un fallo que no dice nada del código y que reaparecería cada pocas pruebas.
        var globalLimitPerMinute = configuration.GetValue("RateLimiting:PermitLimit", 300);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    // Por IP. Aquí ponía «por usuario autenticado; si no, por IP», pero el limitador
                    // corre antes de la autenticación (ver el orden del pipeline en Program): el
                    // usuario nunca estaba autenticado en este punto y esa rama no se ejecutaba jamás.
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = globalLimitPerMinute,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            // Tickets que llegan desde fuera con una clave de entrada. Se reparte por clave y, sin
            // clave, por IP: una ventana única para todos —como la que había— dejaría que el
            // formulario de un cliente con tráfico agotara el cupo de todas las organizaciones.
            options.AddPolicy(TicketingEndpoints.LimiteDeEntrada, context =>
            {
                var key = context.Request.Headers[TicketingEndpoints.CabeceraDeClave].ToString();
                var partition = string.IsNullOrEmpty(key)
                    ? "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "anonymous")
                    : "key:" + ClaveDeEntrada.HashDe(key);

                return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue("EntradaDeTickets:PeticionesPorMinuto", 30),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });

            options.AddFixedWindowLimiter("auth", limiterOptions =>
            {
                limiterOptions.PermitLimit = 10;
                limiterOptions.Window = TimeSpan.FromMinutes(5);
                limiterOptions.QueueLimit = 0;
            });
        });

        return services;
    }
}
