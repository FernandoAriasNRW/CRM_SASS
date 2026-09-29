using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using Tags.Application.Authorization;
using Tags.Application.DTOs;

namespace Tags.Application.Commands;

/// <summary>
/// Crea una etiqueta en la organización de quien llama. <c>TenantId</c> y <c>CreatedBy</c> los pone
/// el endpoint desde <see cref="IUserContext"/>, nunca el cuerpo de la petición.
///
/// <c>Category</c> es obligatoria: una predefinida que no sea automática, o una personalizada de la
/// organización. <c>ColorHex</c> es opcional: sin él se usa un gris neutro.
/// </summary>
public sealed record CreateTagCommand(Guid TenantId, Guid CreatedBy, string Name, string? ColorHex, string Category)
    : ICommand<TagDto>, IAuthorizeEntity, ITagFields
{
    // Las etiquetas son de la organización, no de una entidad concreta: se pide escritura sobre
    // el tipo entero. Así los invitados no crean etiquetas y un permiso por rol sobre «Tag» se
    // aplica si alguien lo define.
    public string EntityType => TagAccess.EntityType;
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";
}
