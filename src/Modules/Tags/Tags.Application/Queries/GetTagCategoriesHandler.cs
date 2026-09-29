using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Queries;
using Tags.Application.BuiltIn;
using Tags.Application.DTOs;
using Tags.Domain.ValueObjects;

namespace Tags.Application.Queries;

/// <summary>Primero las predefinidas, en su orden; después las de la organización, por nombre.</summary>
public sealed class GetTagCategoriesHandler(ITagQueries tags) : IQueryHandler<GetTagCategoriesQuery, List<TagCategoryDto>>
{
    public async Task<Result<List<TagCategoryDto>>> Handle(GetTagCategoriesQuery request, CancellationToken cancellationToken)
    {
        var builtIn = TagCategory.All.Select(c => new TagCategoryDto(
            null, c, BuiltInTags.CategoryLabel(c, request.Language), IsCustom: false, TagCategory.IsAutomatic(c)));

        var custom = await tags.GetCustomCategoriesAsync(request.TenantId, cancellationToken);

        return Result<List<TagCategoryDto>>.Success([.. builtIn, .. custom]);
    }
}
