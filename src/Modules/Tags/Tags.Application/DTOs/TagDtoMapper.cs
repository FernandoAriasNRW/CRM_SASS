using Tags.Application.BuiltIn;
using Tags.Domain.Entities;

namespace Tags.Application.DTOs;

/// <summary>De la entidad al DTO tras crearla o editarla: en español, que es lo guardado.</summary>
public static class TagDtoMapper
{
    public static TagDto From(Tag tag, bool canManage) => new(
        tag.Id, tag.Name, tag.ColorHex, tag.Category, BuiltInTags.CategoryLabel(tag.Category, null),
        tag.ExternalReferenceId, tag.BuiltInKey, tag.CreatedBy, canManage && !tag.IsAutomatic);
}
