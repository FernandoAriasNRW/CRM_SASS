using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;

namespace Identity.Application.Sharing;

public sealed class GetSharedWithHandler(ISharingRepository repository)
    : IQueryHandler<GetSharedWithQuery, IReadOnlyList<Guid>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(GetSharedWithQuery request, CancellationToken ct)
        => Result<IReadOnlyList<Guid>>.Success(
            await repository.GetSharedWithUsersAsync(
                request.TenantId,
                PermissionTypes.FromEntityType(request.EntityType),
                request.EntityId, ct));
}
