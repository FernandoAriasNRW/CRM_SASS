namespace Tags.Presentation.Endpoints;

/// <summary>
/// El cuerpo del alta. No lleva inquilino ni autor: se toman de la sesión, y aceptarlos aquí sería
/// volver a dejar que el cliente elija en qué organización escribe y en nombre de quién.
/// </summary>
public sealed record CreateTagRequest(string? Name, string? ColorHex, string? Category);
