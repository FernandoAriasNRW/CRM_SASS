namespace Tags.Presentation.Endpoints;

/// <summary>
/// El cuerpo del alta. No lleva inquilino: se toma de la sesión, y aceptarlo aquí sería volver a
/// dejar que el cliente elija en qué organización escribe.
/// </summary>
public sealed record CreateTagRequest(string? Name, string? ColorHex, string? Category);
