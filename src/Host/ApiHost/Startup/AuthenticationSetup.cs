using System.Text;
using Identity.Application.Abstractions.Services;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ApiHost.Startup;

public static class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration, string jwtKey)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };

                options.Events = new JwtBearerEvents
                {
                    // El navegador no deja poner cabeceras al abrir un WebSocket ni un
                    // EventSource, así que SignalR manda el token en la cadena de consulta como
                    // `access_token`. Sin leerlo de ahí, los hubs —que exigen autenticación—
                    // respondían 401 y el tiempo real no conectaba nunca. Sólo se acepta en
                    // /hubs: en el resto de la API, un token en la URL acabaría en los registros.
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;

                        return Task.CompletedTask;
                    },


                    // Un refresh token está firmado con la misma clave y tiene el mismo
                    // issuer/audience que un access token, así que pasaría la validación
                    // estándar. Sólo el claim token_type los distingue: sin esta
                    // comprobación, un refresh token vale como credencial de acceso
                    // durante los 7 días de su vigencia.
                    //
                    // Aquí había una excepción para los tokens con rol «Guest», que no llevaban
                    // token_type: eran los del token de invitado anónimo. Ese token ya no existe,
                    // y la excepción sólo dejaba una puerta sin motivo.
                    OnTokenValidated = context =>
                    {
                        var tokenType = context.Principal?.FindFirst(JwtService.TokenTypeClaim)?.Value;

                        if (tokenType != JwtService.AccessTokenType)
                            context.Fail("Se requiere un access token.");

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<BuildingBlocks.Application.Abstractions.IUserContext, Services.UserContext>();

        return services;
    }
}
