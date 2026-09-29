namespace Tags.Application.BuiltIn;

/// <summary>
/// El idioma lo manda la pantalla (<c>?language=en</c>), no se deduce del <c>Accept-Language</c>:
/// la traducción de la interfaz es en tiempo de compilación, así que cada paquete sabe en qué
/// idioma está y el navegador no (el mismo criterio que <c>Docs.Application.Templates.BuiltInTemplates</c>).
/// Cualquier otro valor, o ninguno, es español.
/// </summary>
public static class Languages
{
    public static bool IsEnglish(string? language) => string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
}
