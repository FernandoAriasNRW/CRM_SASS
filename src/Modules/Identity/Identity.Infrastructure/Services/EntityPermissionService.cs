using BuildingBlocks.Application.Authorization;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public sealed class EntityPermissionService(IdentityDbContext context) : IEntityPermissionService
{
    public async Task<bool> HasPermissionAsync(
        Guid tenantId, 
        Guid userId, 
        string entityType, 
        Guid entityId, 
        string requiredPermission, 
        CancellationToken cancellationToken = default)
    {
        var user = await context.User.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);
        if (user == null) return false;
        
        if (user.Role == Identity.Domain.ValueObjects.UserRole.Admin)
            return true;

        // Check user-specific permission first
        var userPermission = await context.EntityPermissions
            .FirstOrDefaultAsync(p => 
                p.TenantId == tenantId && 
                p.UserId == userId && 
                p.EntityType == entityType && 
                (p.EntityId == entityId || p.EntityId == Guid.Empty), 
                cancellationToken);

        if (userPermission != null)
        {
            return EvaluateLevel(userPermission.PermissionLevel, requiredPermission);
        }

        // Check role-specific default permission
        var rolePermission = await context.EntityPermissions
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId &&
                p.RoleName == user.Role.Name &&
                p.EntityType == entityType &&
                (p.EntityId == entityId || p.EntityId == Guid.Empty),
                cancellationToken);

        if (rolePermission != null)
        {
            return EvaluateLevel(rolePermission.PermissionLevel, requiredPermission);
        }

        // Standard members have default Read/Write access unless explicitly restricted
        if (user.Role == Identity.Domain.ValueObjects.UserRole.Member)
        {
            if (requiredPermission is "Read" or "Write")
                return true;
        }

        return false;
    }

    private static bool EvaluateLevel(string level, string required)
    {
        if (level == "None") return false;
        if (level == "Full" || level == "Admin") return true;

        return required switch
        {
            "Read" or "View" => level is "Read" or "View" or "Write" or "Edit" or "Full" or "Admin",
            "Write" or "Edit" => level is "Write" or "Edit" or "Full" or "Admin",
            "Admin" or "Full" => level is "Full" or "Admin",
            _ => false
        };
    }
}
