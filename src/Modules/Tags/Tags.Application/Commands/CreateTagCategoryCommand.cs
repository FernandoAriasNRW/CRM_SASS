using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using Tags.Application.DTOs;

namespace Tags.Application.Commands;

/// <summary>Crea una categoría propia de la organización. <c>TenantId</c> sale de la sesión.</summary>
public sealed record CreateTagCategoryCommand(Guid TenantId, string Name)
    : ICommand<TagCategoryDto>, IAuthorizeEntity
{
    // El mismo permiso que crear una etiqueta: quien puede etiquetar puede ordenar sus etiquetas.
    public string EntityType => "Tag";
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";
}
