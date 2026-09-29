namespace Tags.Application.DTOs;

/// <summary>
/// Una etiqueta tal como sale por la API, con el nombre ya en el idioma pedido si es predefinida.
///
/// <c>ExternalReferenceId</c> sólo lo llevan las que nacieron solas con un proyecto o un equipo
/// (ver <c>ApiHost/Tags/EtiquetasAutomaticas.cs</c>); <c>BuiltInKey</c>, las que trae el producto.
/// </summary>
public sealed record TagDto(
    Guid Id,
    string Name,
    string ColorHex,
    string Category,
    string CategoryLabel,
    Guid? ExternalReferenceId,
    string? BuiltInKey);
