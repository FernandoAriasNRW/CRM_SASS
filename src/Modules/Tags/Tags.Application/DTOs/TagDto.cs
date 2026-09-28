namespace Tags.Application.DTOs;

/// <summary>
/// Una etiqueta tal como sale por la API. <c>ExternalReferenceId</c> sólo lo llevan las que
/// nacieron solas con un proyecto o un equipo (ver <c>ApiHost/Tags/EtiquetasAutomaticas.cs</c>).
/// </summary>
public sealed record TagDto(Guid Id, string Name, string ColorHex, string Category, Guid? ExternalReferenceId);
