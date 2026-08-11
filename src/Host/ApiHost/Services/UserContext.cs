using System.Security.Claims;
using BuildingBlocks.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace ApiHost.Services;

public sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    
    public Guid TenantId => Guid.TryParse(User?.FindFirstValue("TenantId"), out var id) ? id : Guid.Empty;
    
    public string Role => User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}
