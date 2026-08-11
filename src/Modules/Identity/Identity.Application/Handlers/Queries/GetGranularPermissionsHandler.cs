using BuildingBlocks.Domain;
using Identity.Application.Abstractions.Repositories;
using Identity.Application.Queries;
using MediatR;

namespace Identity.Application.Handlers.Queries;

public class GetGranularPermissionsHandler(IEntityPermissionRepository permissionRepository)
    : IRequestHandler<GetGranularPermissionsQuery, Result<List<GranularPermissionDto>>>
{
    public async Task<Result<List<GranularPermissionDto>>> Handle(GetGranularPermissionsQuery request, CancellationToken cancellationToken)
    {
        var entities = await permissionRepository.GetPermissionsAsync(
            request.TenantId,
            request.TargetType,
            request.TargetId,
            request.RoleName,
            cancellationToken);

        var permissions = entities
            .Select(p => new GranularPermissionDto(
                p.Id,
                p.TargetType,
                p.UserId,
                p.TeamId,
                p.RoleName,
                p.EntityType,
                p.EntityId,
                p.PermissionLevel))
            .ToList();

        return Result<List<GranularPermissionDto>>.Success(permissions);
    }
}
