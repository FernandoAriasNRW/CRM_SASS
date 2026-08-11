using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Identity.Application.Abstractions.Services;
using Identity.Application.DTOs;

namespace Identity.Infrastructure.Services;

public sealed class JwtService : IJwtService
{
    /// <summary>Claim que distingue un access token de un refresh token.</summary>
    public const string TokenTypeClaim = "token_type";
    public const string AccessTokenType = "access";
    public const string RefreshTokenType = "refresh";

    private readonly IConfiguration _config;
    private readonly SymmetricSecurityKey _securityKey;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtService(IConfiguration config)
    {
        _config = config;

        var keyString = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key no está configurada en appsettings.json");

        if (keyString.Length < 32)
            throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres para HMAC-SHA256");

        _securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        _issuer = _config["Jwt:Issuer"] ?? "crm-saas-api";
        _audience = _config["Jwt:Audience"] ?? "crm-saas-web";
    }

    public (string accessToken, DateTime accessExpires, string refreshToken, DateTime refreshExpires) GenerateTokens(UserDto user)
    {
        var credentials = new SigningCredentials(_securityKey, SecurityAlgorithms.HmacSha256);

        // 'jti' único por token: sin él, dos llamadas dentro del mismo segundo
        // producen tokens byte a byte idénticos (sólo varía 'exp', con resolución
        // de segundos), lo que hace imposible revocarlos o auditarlos por separado.
        var accessClaims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(TokenTypeClaim, AccessTokenType),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.Name),
            new Claim("tenantId", user.TenantId.ToString()),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var accessExpires = DateTime.UtcNow.AddMinutes(15);
        var accessToken = new JwtSecurityToken(_issuer, _audience, accessClaims, expires: accessExpires, signingCredentials: credentials);

        // El refresh token NO debe llevar los mismos claims que el access token.
        // Si los lleva, es aceptado por el middleware de autenticación y se
        // convierte de facto en un access token con 7 días de vigencia y el rol
        // del usuario. Lleva sólo lo imprescindible para reemitir la sesión.
        var refreshClaims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(TokenTypeClaim, RefreshTokenType),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("tenantId", user.TenantId.ToString())
        };

        var refreshExpires = DateTime.UtcNow.AddDays(7);
        var refreshTokenJwt = new JwtSecurityToken(_issuer, _audience, refreshClaims, expires: refreshExpires, signingCredentials: credentials);
        var refreshToken = new JwtSecurityTokenHandler().WriteToken(refreshTokenJwt);

        return (
            new JwtSecurityTokenHandler().WriteToken(accessToken),
            accessExpires,
            refreshToken,
            refreshExpires
        );
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _securityKey,
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return tokenHandler.ReadJwtToken(token).Payload.Claims.Count() > 0 
                   ? new ClaimsPrincipal(new ClaimsIdentity(tokenHandler.ReadJwtToken(token).Claims, "jwt")) 
                   : null;
        }
        catch
        {
            return null;
        }
    }

    public string GenerateGuestToken(Guid tenantId, string tenantSlug)
    {
        var credentials = new SigningCredentials(_securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim("tenantId", tenantId.ToString()),
            new Claim(ClaimTypes.Role, "Guest"),
            new Claim("scope", "tickets:create"),
            new Claim("tenantSlug", tenantSlug)
        };

        var expires = DateTime.UtcNow.AddMinutes(15);
        var token = new JwtSecurityToken(_issuer, _audience, claims, expires: expires, signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
