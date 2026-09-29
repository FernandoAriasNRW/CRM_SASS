using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Repositories;
using Tags.Application.BuiltIn;
using Tags.Application.DTOs;
using Tags.Domain.Entities;

namespace Tags.Application.Commands;

public sealed class CreateTagHandler(ITagRepository tags) : ICommandHandler<CreateTagCommand, TagDto>
{
    /// <summary>El color de una etiqueta a la que no se le da ninguno: gris, que no compite con los demás.</summary>
    public const string DefaultColorHex = "#6B7280";

    /// <summary>El único fallo que devuelve es un nombre repetido en la categoría: lo demás lo para el validador.</summary>
    public async Task<Result<TagDto>> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var category = request.Category.Trim();

        // Una predefinida se guarda en español; «VIP client» en inglés sería la misma etiqueta
        // repetida con otro nombre, y el índice único no lo vería.
        var repeatsBuiltIn = BuiltInTags.All.Any(t => t.Category == category
            && (string.Equals(t.SpanishName, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(t.EnglishName, name, StringComparison.OrdinalIgnoreCase)));

        if (repeatsBuiltIn || await tags.ExistsByNameAsync(request.TenantId, category, name, cancellationToken))
            return Result<TagDto>.Failure($"Ya existe una etiqueta llamada «{name}» en esa categoría");

        var tag = Tag.Create(
            request.TenantId,
            name,
            string.IsNullOrEmpty(request.ColorHex) ? DefaultColorHex : request.ColorHex.ToUpperInvariant(),
            category);

        await tags.AddAsync(tag, cancellationToken);

        return Result<TagDto>.Success(new TagDto(
            tag.Id, tag.Name, tag.ColorHex, tag.Category, BuiltInTags.CategoryLabel(tag.Category, null),
            tag.ExternalReferenceId, tag.BuiltInKey));
    }
}
