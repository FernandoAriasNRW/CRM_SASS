using System.Security.Claims;
using BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace ApiHost.Services;

public sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    /// <summary>
    /// Nombre del claim del tenant, tal como lo emite <c>JwtService</c>.
    /// </summary>
    public const string ClaimDeTenant = "tenantId";

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    /// <summary>
    /// Tenant de la petición en curso, del que depende el filtro global de aislamiento.
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
    public Guid TenantId
    {
        get
        {
            var claim = User?.Claims.FirstOrDefault(
                c => string.Equals(c.Type, ClaimDeTenant, StringComparison.OrdinalIgnoreCase));

            return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
        }
    }

    public string Role => User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}
