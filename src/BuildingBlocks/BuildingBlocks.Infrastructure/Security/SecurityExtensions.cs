using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Infrastructure.Security;

public static class SecurityExtensions
{
  public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
  {

    // 2. Autenticación JWT (Se mantiene igual, es sólida)
    var jwtKey = configuration["Jwt:Key"] ?? throw new Exception("JWT Key missing");

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
          };
        });

    services.AddAuthorization();
    return services;
  }
}