using Identity.Domain.Entities;

namespace Identity.Application.DTOs;

public sealed record UserDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Email,
    string Role,
    string? PasswordHash = null,
    bool IsActive = true,
    DateTime? CreatedAt = null,
    string? AvatarUrl = null,
    string? PhoneNumber = null,
    string? Bio = null
)
{
    // ✅ Factory method: Create from Domain Entity
    public static UserDto FromEntity(User user) => new(
        user.Id,
        user.TenantId,
        user.Name,
        user.Email.Value,
        user.Role.Name,
        user.PasswordHash?.Value,
        true,
        user.CreatedAtUtc,
        user.AvatarUrl,
        user.PhoneNumber,
        user.Bio
    );

}

public sealed record LoginResult(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc
);

public sealed record GuestTokenResult(string AccessToken, DateTime ExpiresAtUtc);
