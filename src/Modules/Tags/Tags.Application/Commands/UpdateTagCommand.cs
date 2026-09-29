using BuildingBlocks.Application.Abstractions;
using Tags.Application.DTOs;

namespace Tags.Application.Commands;

/// <summary>
/// Cambia nombre, color y categoría de una etiqueta. Sin <c>ColorHex</c> se conserva el que tenía.
///
/// No implementa <c>IAuthorizeEntity</c> a propósito: el comportamiento de autorización sólo sabe
/// preguntar por permisos, y aquí también cuenta quién la creó. Lo comprueba el handler con
/// <see cref="Authorization.TagAccess"/>.
/// </summary>
public sealed record UpdateTagCommand(Guid TenantId, Guid UserId, Guid TagId, string Name, string? ColorHex, string Category)
    : ICommand<TagDto>, ITagFields;
