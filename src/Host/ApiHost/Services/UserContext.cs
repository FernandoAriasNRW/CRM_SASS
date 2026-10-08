using System.Security.Claims;
using BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace ApiHost.Services;

/// <summary>
/// El usuario de la petición en curso. Las reglas de lectura de cada claim están en
/// <see cref="UserClaims"/>, que comparten los hubs de SignalR.
/// </summary>
public sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    /// <summary>
    /// Nombre del claim del tenant, tal como lo emite <c>JwtService</c>.
    /// </summary>
    public const string TenantClaim = UserClaims.TenantClaim;

    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid UserId => UserClaims.UserId(User);

    /// <inheritdoc cref="UserClaims.TenantId"/>
    public Guid TenantId => UserClaims.TenantId(User);

    public string Role => UserClaims.Role(User);
}
