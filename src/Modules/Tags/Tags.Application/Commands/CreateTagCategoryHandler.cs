using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Repositories;
using Tags.Application.DTOs;
using Tags.Domain.Entities;

namespace Tags.Application.Commands;

public sealed class CreateTagCategoryHandler(ITagCategoryRepository categories)
    : ICommandHandler<CreateTagCategoryCommand, TagCategoryDto>
{
    /// <summary>El único fallo que devuelve es un nombre repetido: lo demás lo para el validador.</summary>
    public async Task<Result<TagCategoryDto>> Handle(CreateTagCategoryCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await categories.ExistsAsync(request.TenantId, name, cancellationToken))
            return Result<TagCategoryDto>.Failure($"Ya existe una categoría llamada «{name}»");

        var category = CustomTagCategory.Create(request.TenantId, name);
        await categories.AddAsync(category, cancellationToken);

        return Result<TagCategoryDto>.Success(
            new TagCategoryDto(category.Id, category.Name, category.Name, IsCustom: true, IsAutomatic: false));
    }
}
