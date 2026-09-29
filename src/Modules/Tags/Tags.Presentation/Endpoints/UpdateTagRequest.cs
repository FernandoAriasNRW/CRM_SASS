namespace Tags.Presentation.Endpoints;

/// <summary>
/// El cuerpo de la edición: la etiqueta entera, como en un PUT. Sin <c>ColorHex</c> se conserva el
/// que tenía.
/// </summary>
public sealed record UpdateTagRequest(string? Name, string? ColorHex, string? Category);
