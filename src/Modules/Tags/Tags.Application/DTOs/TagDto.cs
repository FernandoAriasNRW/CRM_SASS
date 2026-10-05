namespace Tags.Application.DTOs;

/// <summary>
/// Una etiqueta tal como sale por la API, con el nombre ya en el idioma pedido si es predefinida.
///
/// <c>ExternalReferenceId</c> sólo lo llevan las que nacieron solas con un proyecto o un equipo
/// (ver <c>ApiHost/Tags/AutomaticTags.cs</c>); <c>BuiltInKey</c>, las que trae el producto;
/// <c>CreatedBy</c>, las que creó una persona. <c>CanManage</c> dice si quien pregunta puede
/// editarla y borrarla (ver <c>Authorization.TagAccess</c>), para que la pantalla no ofrezca botones
/// que van a dar 403.
/// </summary>
public sealed record TagDto(
    Guid Id,
    string Name,
    string ColorHex,
    string Category,
    string CategoryLabel,
    Guid? ExternalReferenceId,
    string? BuiltInKey,
    Guid? CreatedBy,
    bool CanManage);
