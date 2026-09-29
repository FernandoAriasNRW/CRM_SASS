using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Queries;
using Tags.Application.BuiltIn;
using Tags.Application.DTOs;

namespace Tags.Application.Queries;

public sealed class GetTagsHandler(ITagQueries tags) : IQueryHandler<GetTagsQuery, List<TagDto>>
{
    public async Task<Result<List<TagDto>>> Handle(GetTagsQuery request, CancellationToken cancellationToken)
    {
        var stored = await tags.GetByTenantAsync(request.TenantId, cancellationToken);

        return Result<List<TagDto>>.Success(stored
            .Select(t => t with
            {
                Name = BuiltInTags.Find(t.BuiltInKey)?.NameIn(request.Language) ?? t.Name,
                CategoryLabel = BuiltInTags.CategoryLabel(t.Category, request.Language),
            })
            .ToList());
    }
}
