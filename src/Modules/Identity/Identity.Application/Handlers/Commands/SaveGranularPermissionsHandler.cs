using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Application.Abstractions.Repositories;
using Identity.Application.Commands;
using Identity.Domain.Entities;
using MediatR;

namespace Identity.Application.Handlers.Commands;

public class SaveGranularPermissionsHandler(
    IEntityPermissionRepository permissionRepository,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<SaveGranularPermissionsCommand, Result>
{
    public async Task<Result> Handle(SaveGranularPermissionsCommand request, CancellationToken cancellationToken)
    {
        var targetType = string.IsNullOrWhiteSpace(request.TargetType) ? "User" : request.TargetType;
        Guid? targetId = targetType == "User" ? request.UserId : (targetType == "Team" ? request.TeamId : null);

        var existingPermissions = await permissionRepository.GetPermissionsAsync(
            request.TenantId,
            targetType,
            targetId,
            request.RoleName,
            cancellationToken);

        foreach (var item in request.Permissions)
        {
            var existing = existingPermissions.FirstOrDefault(p => 
                p.EntityType == item.EntityType && p.EntityId == item.EntityId);

            if (existing != null)
            {
                existing.UpdatePermissionLevel(item.PermissionLevel);
            }
            else
            {
                EntityPermission newPerm;
                if (targetType == "User" && request.UserId.HasValue)
                {
                    newPerm = EntityPermission.CreateForUser(request.TenantId, request.UserId.Value, item.EntityType, item.EntityId, item.PermissionLevel);
                }
                else if (targetType == "Team" && request.TeamId.HasValue)
                {
                    newPerm = EntityPermission.CreateForTeam(request.TenantId, request.TeamId.Value, item.EntityType, item.EntityId, item.PermissionLevel);
                }
                else if (targetType == "Role" && !string.IsNullOrWhiteSpace(request.RoleName))
                {
                    newPerm = EntityPermission.CreateForRole(request.TenantId, request.RoleName, item.EntityType, item.EntityId, item.PermissionLevel);
                }
                else
                {
                    continue;
                }

                await permissionRepository.AddAsync(newPerm, cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
