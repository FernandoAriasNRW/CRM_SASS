using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Identity.Application.Commands;

public record GranularPermissionInputItem(
    string EntityType,
    Guid EntityId,
    string PermissionLevel
);

public record SaveGranularPermissionsCommand(
    Guid TenantId,
    string TargetType, // "User", "Team", "Role"
    Guid? UserId,
    Guid? TeamId,
    string? RoleName,
    List<GranularPermissionInputItem> Permissions
) : IRequest<Result>;
