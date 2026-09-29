using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Repositories;
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

        if (await tags.NameIsTakenAsync(request.TenantId, category, name, exceptTag: null, cancellationToken))
            return Result<TagDto>.Failure($"Ya existe una etiqueta llamada «{name}» en esa categoría");

        var tag = Tag.Create(
            request.TenantId,
            name,
            string.IsNullOrEmpty(request.ColorHex) ? DefaultColorHex : request.ColorHex.ToUpperInvariant(),
            category,
            createdBy: request.CreatedBy);

        await tags.AddAsync(tag, cancellationToken);

        // Quien la crea puede gestionarla siempre.
        return Result<TagDto>.Success(TagDtoMapper.From(tag, canManage: true));
    }
}
