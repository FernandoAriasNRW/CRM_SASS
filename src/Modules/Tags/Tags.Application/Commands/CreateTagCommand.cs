using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using Tags.Application.DTOs;

namespace Tags.Application.Commands;

/// <summary>
/// Crea una etiqueta en la organización de quien llama. <c>TenantId</c> lo pone el endpoint desde
/// <see cref="IUserContext"/>, nunca el cuerpo de la petición.
///
/// <c>ColorHex</c> y <c>Category</c> son opcionales: sin color se usa un gris neutro y sin
/// categoría, <see cref="Tags.Domain.ValueObjects.TagCategory.General"/>.
/// </summary>
public sealed record CreateTagCommand(Guid TenantId, string Name, string? ColorHex, string? Category)
    : ICommand<TagDto>, IAuthorizeEntity
{
    // Las etiquetas son de la organización, no de una entidad concreta: se pide escritura sobre
    // el tipo entero. Así los invitados no crean etiquetas y un permiso por rol sobre «Tag» se
    // aplica si alguien lo define.
    public string EntityType => "Tag";
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";
}
