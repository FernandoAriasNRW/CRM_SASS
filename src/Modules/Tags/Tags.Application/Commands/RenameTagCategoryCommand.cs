using BuildingBlocks.Application.Abstractions;
using Tags.Application.DTOs;

namespace Tags.Application.Commands;

/// <summary>
/// Renombra una categoría propia y mueve sus etiquetas. Lo puede quien gestiona todas las
/// etiquetas (administrador o permiso «Full»): afecta a etiquetas de otras personas. La
/// autorización la hace el handler con <c>TagAccess</c>.
/// </summary>
public sealed record RenameTagCategoryCommand(Guid TenantId, Guid UserId, Guid CategoryId, string Name)
    : ICommand<TagCategoryDto>;
