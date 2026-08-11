using Identity.Application.DTOs;
using Identity.Domain.ValueObjects;

namespace Identity.Application.Abstractions.Services;

public interface IJwtService
{
  (string accessToken, DateTime accessExpires, string refreshToken, DateTime refreshExpires) GenerateTokens(UserDto user);

  string GenerateGuestToken(Guid tenantId, string tenantSlug);

  System.Security.Claims.ClaimsPrincipal? ValidateToken(string token);
}

public interface IPasswordHasher
{
  PasswordHash CreatePasswordHash(string plainPassword);

  bool VerifyPassword(string plainPassword, PasswordHash passwordHash);
}