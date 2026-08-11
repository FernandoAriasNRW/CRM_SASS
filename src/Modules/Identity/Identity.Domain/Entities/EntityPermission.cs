using BuildingBlocks.Domain.Primitives;

namespace Identity.Domain.Entities;

public sealed class EntityPermission : AggregateRoot, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? TeamId { get; private set; }
    public string? RoleName { get; private set; }
    public string TargetType { get; private set; } = "User"; // "User", "Team", "Role"
    public string EntityType { get; private set; } = string.Empty; // "Projects", "Tasks", "Docs", "Webhooks", "Teams", "Reports", "Settings"
    public Guid EntityId { get; private set; } // Guid.Empty for module level
    public string PermissionLevel { get; private set; } = string.Empty; // "Full", "Edit", "View", "None"

    private EntityPermission() { }

    public static EntityPermission CreateForUser(Guid tenantId, Guid userId, string entityType, Guid entityId, string permissionLevel)
    {
        return new EntityPermission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            TeamId = null,
            RoleName = null,
            TargetType = "User",
            EntityType = entityType,
            EntityId = entityId,
            PermissionLevel = permissionLevel
        };
    }

    public static EntityPermission CreateForTeam(Guid tenantId, Guid teamId, string entityType, Guid entityId, string permissionLevel)
    {
        return new EntityPermission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = null,
            TeamId = teamId,
            RoleName = null,
            TargetType = "Team",
            EntityType = entityType,
            EntityId = entityId,
            PermissionLevel = permissionLevel
        };
    }

    public static EntityPermission CreateForRole(Guid tenantId, string roleName, string entityType, Guid entityId, string permissionLevel)
    {
        return new EntityPermission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = null,
            TeamId = null,
            RoleName = roleName,
            TargetType = "Role",
            EntityType = entityType,
            EntityId = entityId,
            PermissionLevel = permissionLevel
        };
    }

    public void UpdatePermissionLevel(string newLevel)
    {
        PermissionLevel = newLevel;
    }
}
