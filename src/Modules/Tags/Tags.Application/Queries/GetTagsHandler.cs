using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Queries;
using Tags.Application.DTOs;

namespace Tags.Application.Queries;

public sealed class GetTagsHandler(ITagQueries tags) : IQueryHandler<GetTagsQuery, List<TagDto>>
{
    public async Task<Result<List<TagDto>>> Handle(GetTagsQuery request, CancellationToken cancellationToken)
        => Result<List<TagDto>>.Success(await tags.GetByTenantAsync(request.TenantId, cancellationToken));
}
