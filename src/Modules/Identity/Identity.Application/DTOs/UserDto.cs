using Identity.Domain.Entities;

namespace Identity.Application.DTOs;

/// <summary>
/// El usuario tal como sale de la API.
///
/// **Sin el hash de la contraseña.** Este DTO lo llevaba, y como es lo que devuelve
/// <c>GET /api/v1/auth/users/me</c>, el hash bcrypt del usuario viajaba al navegador en cada
/// arranque de la aplicación, quedándose por el camino en cachés y registros. Nadie lo
/// necesitaba: quien verifica la contraseña al iniciar sesión trabaja con la entidad de
/// dominio que devuelve el repositorio, no con este DTO, y el emisor de tokens tampoco lo
/// leía nunca.
///
/// Un dato que no sale de aquí no se puede filtrar por descuido más adelante, así que se
/// quita el campo en vez de confiar en que cada endpoint se acuerde de no serializarlo.
/// </summary>
public sealed record UserDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Email,
    string Role,
    bool IsActive = true,
    DateTime? CreatedAt = null,
    string? AvatarUrl = null,
    string? PhoneNumber = null,
    string? Bio = null
)
{
    public static UserDto FromEntity(User user) => new(
        user.Id,
        user.TenantId,
        user.Name,
        user.Email.Value,
        user.Role.Name,
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
