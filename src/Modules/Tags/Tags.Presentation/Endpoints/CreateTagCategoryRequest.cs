namespace Tags.Presentation.Endpoints;

/// <summary>El cuerpo del alta de una categoría. Sin inquilino: se toma de la sesión.</summary>
public sealed record CreateTagCategoryRequest(string? Name);
