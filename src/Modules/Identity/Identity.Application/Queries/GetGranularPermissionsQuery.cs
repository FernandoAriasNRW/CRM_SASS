using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Identity.Application.Queries;

public record GranularPermissionDto(
    Guid Id,
    string TargetType, // "User", "Team", "Role"
    Guid? UserId,
    Guid? TeamId,
    string? RoleName,
    string EntityType, // "Projects", "Tasks", "Docs", "Webhooks", "Teams", "Reports", "Settings"
    Guid EntityId,
    string PermissionLevel // "Full", "Edit", "View", "None"
);

public record GetGranularPermissionsQuery(
    Guid TenantId,
    string? TargetType = null,
    Guid? TargetId = null,
    string? RoleName = null
) : IRequest<Result<List<GranularPermissionDto>>>;
