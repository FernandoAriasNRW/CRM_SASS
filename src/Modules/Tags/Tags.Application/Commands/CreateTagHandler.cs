using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Repositories;
using Tags.Application.DTOs;
using Tags.Domain.Entities;
using Tags.Domain.ValueObjects;

namespace Tags.Application.Commands;

public sealed class CreateTagHandler(ITagRepository tags) : ICommandHandler<CreateTagCommand, TagDto>
{
    /// <summary>El color de una etiqueta a la que no se le da ninguno: gris, que no compite con los demás.</summary>
    public const string DefaultColorHex = "#6B7280";

    public async Task<Result<TagDto>> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await tags.ExistsByNameAsync(request.TenantId, name, cancellationToken))
            return Result<TagDto>.Failure($"Ya existe una etiqueta llamada «{name}»");

        var tag = Tag.Create(
            request.TenantId,
            name,
            string.IsNullOrEmpty(request.ColorHex) ? DefaultColorHex : request.ColorHex.ToUpperInvariant(),
            string.IsNullOrWhiteSpace(request.Category) ? TagCategory.General : request.Category.Trim());

        await tags.AddAsync(tag, cancellationToken);

        return Result<TagDto>.Success(new TagDto(tag.Id, tag.Name, tag.ColorHex, tag.Category, tag.ExternalReferenceId));
    }
}
