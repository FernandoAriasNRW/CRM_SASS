using System.Security.Claims;

namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Cómo se leen el usuario, el inquilino y el rol de un token ya validado.
///
/// Lo normal es no usar esto directamente sino <see cref="IUserContext"/>, que lo aplica a la
/// petición en curso. Existe aparte por los hubs de SignalR: una invocación a un hub no es una
/// petición HTTP y no tiene un <c>HttpContext</c> fiable, pero sí <c>Context.User</c>. Así el hub
/// lee el inquilino con la misma regla que el resto de la API, en vez de buscar el claim a mano.
/// </summary>
public static class UserClaims
{
    /// <summary>
    /// Nombre del claim del tenant, tal como lo emite <c>JwtService</c>.
    /// </summary>
    public const string TenantClaim = "tenantId";

    public static Guid UserId(ClaimsPrincipal? user) =>
        Guid.TryParse(user?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : Guid.Empty;

    /// <summary>
    /// Tenant del token, del que depende el filtro global de aislamiento.
    ///
    /// La búsqueda es insensible a mayúsculas y se hace a mano a propósito. Esto buscaba el
    /// claim como «TenantId» mientras el token lo emite como «tenantId», y las identidades de
    /// JWT de este stack —<c>CaseSensitiveClaimsIdentity</c>, de Microsoft.IdentityModel 8—
    /// **distinguen mayúsculas** al buscar claims, al contrario que un <c>ClaimsIdentity</c>
    /// normal. El claim nunca se encontraba, el tenant era <c>Guid.Empty</c> en todas las
    /// peticiones y el filtro global —que cierra por defecto— dejaba **todas** las consultas
    /// sin resultados, sin dar ningún error. Los endpoints no lo sufrían porque leen
    /// «tenantId» con el mismo nombre que el token.
    ///
    /// Se recorren los claims comparando sin distinguir mayúsculas para que la aplicación no
    /// vuelva a quedarse ciega si alguien cambia el nombre del claim al emitirlo.
    /// </summary>
    public static Guid TenantId(ClaimsPrincipal? user)
    {
        var claim = user?.Claims.FirstOrDefault(
            c => string.Equals(c.Type, TenantClaim, StringComparison.OrdinalIgnoreCase));

        return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
    }

    public static string Role(ClaimsPrincipal? user) => user?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
}
