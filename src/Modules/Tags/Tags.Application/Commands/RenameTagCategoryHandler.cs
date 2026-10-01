using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Repositories;
using Tags.Application.Authorization;
using Tags.Application.DTOs;

namespace Tags.Application.Commands;

public sealed class RenameTagCategoryHandler(ITagCategoryRepository categories, IEntityPermissionService permissions)
    : ICommandHandler<RenameTagCategoryCommand, TagCategoryDto>
{
    /// <summary>404 si no existe, 403 si no puede, y el fallo que devuelve (409) es un nombre repetido.</summary>
    public async Task<Result<TagCategoryDto>> Handle(RenameTagCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.TenantId, request.CategoryId, cancellationToken)
            ?? throw new KeyNotFoundException($"No existe la categoría {request.CategoryId}");

        await TagAccess.EnsureCanManageAllAsync(permissions, request.TenantId, request.UserId, cancellationToken);

        var name = request.Name.Trim();
        if (await categories.ExistsAsync(request.TenantId, name, category.Id, cancellationToken))
            return Result<TagCategoryDto>.Failure($"Ya existe una categoría llamada «{name}»");

        await categories.RenameAsync(category, name, cancellationToken);
        var count = await categories.CountTagsAsync(request.TenantId, category.Name, cancellationToken);

        return Result<TagCategoryDto>.Success(new TagCategoryDto(
            category.Id, category.Name, category.Name, IsCustom: true, IsAutomatic: false, count, CanManage: true));
    }
}
