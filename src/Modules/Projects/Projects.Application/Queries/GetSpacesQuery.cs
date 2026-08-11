using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Application.Abstractions.Queries;
using Projects.Application.DTOs;

namespace Projects.Application.Queries;

public sealed record GetSpacesQuery(Guid TenantId) : IQuery<IEnumerable<SpaceDto>>;

public sealed class GetSpacesQueryHandler(IProjectQueries queries) : IQueryHandler<GetSpacesQuery, IEnumerable<SpaceDto>>
{
    public async Task<Result<IEnumerable<SpaceDto>>> Handle(GetSpacesQuery request, CancellationToken cancellationToken)
    {
        var spaces = await queries.GetSpacesAsync(request.TenantId, cancellationToken);
        return Result<IEnumerable<SpaceDto>>.Success(spaces);
    }
}
