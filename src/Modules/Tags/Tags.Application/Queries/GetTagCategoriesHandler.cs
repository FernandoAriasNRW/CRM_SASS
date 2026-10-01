using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Queries;
using Tags.Application.Authorization;
using Tags.Application.BuiltIn;
using Tags.Application.DTOs;
using Tags.Domain.ValueObjects;

namespace Tags.Application.Queries;

/// <summary>Primero las predefinidas, en su orden; después las de la organización, por nombre.</summary>
public sealed class GetTagCategoriesHandler(ITagQueries tags, IEntityPermissionService permissions)
    : IQueryHandler<GetTagCategoriesQuery, List<TagCategoryDto>>
{
    public async Task<Result<List<TagCategoryDto>>> Handle(GetTagCategoriesQuery request, CancellationToken cancellationToken)
    {
        var counts = await tags.CountByCategoryAsync(request.TenantId, cancellationToken);
        int CountOf(string category) => counts.TryGetValue(category, out var n) ? n : 0;

        // Renombrar o borrar una categoría propia afecta a todas sus etiquetas, sean de quien sean:
        // lo puede quien gestiona todas, no sólo las suyas.
        var canManageAll = await TagAccess.CanManageAllAsync(permissions, request.TenantId, request.UserId, cancellationToken);

        var builtIn = TagCategory.All.Select(c => new TagCategoryDto(
            null, c, BuiltInTags.CategoryLabel(c, request.Language), IsCustom: false, TagCategory.IsAutomatic(c), CountOf(c)));

        var custom = (await tags.GetCustomCategoriesAsync(request.TenantId, cancellationToken))
            .Select(c => c with { TagCount = CountOf(c.Name), CanManage = canManageAll });

        return Result<List<TagCategoryDto>>.Success([.. builtIn, .. custom]);
    }
}
