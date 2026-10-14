namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Qué se puede subir al almacenamiento y cómo se sirve.
///
/// <b>Los ficheros subidos se sirven desde el mismo origen que la API</b> (almacenamiento en disco,
/// <c>/storage</c>), y la cookie de refresco de sesión es de ese origen. Un <c>.html</c> subido y
/// abierto ahí podía llamar a <c>/api/v1/auth/refresh</c> con la cookie de quien lo abriera y
/// leer su token: cualquiera con sesión subía la página, mandaba el enlace a quien administra y
/// se quedaba con su cuenta.
///
/// Se cierra por dos lados: al servirlos (<see cref="ServingHeaders"/>, la defensa principal,
/// que vale también para lo que ya estuviera subido) y al subirlos (<see cref="RejectionReason"/>).
/// </summary>
public static class UploadRules
{
    /// <summary>Tope por fichero. Las imágenes y adjuntos de un documento no necesitan más.</summary>
    public const long MaxBytes = 25L * 1024 * 1024;

    /// <summary>
    /// Lo que un navegador ejecuta si lo abre: páginas, scripts y SVG (que puede llevar scripts).
    /// No se admiten al subir y, si ya estaban, se sirven como descarga.
    /// </summary>
    public static readonly IReadOnlySet<string> ExecutableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".xhtml", ".shtml", ".xht", ".mht", ".mhtml",
        ".svg", ".svgz", ".xml", ".xsl", ".xslt",
        ".js", ".mjs", ".cjs",
    };

    public static string? RejectionReason(string? fileName, long length)
    {
        if (length <= 0) return "El fichero está vacío.";
        if (length > MaxBytes) return $"El fichero supera el máximo de {MaxBytes / (1024 * 1024)} MB.";

        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (ExecutableExtensions.Contains(extension))
            return $"No se pueden subir ficheros {extension}: el navegador los ejecutaría al abrirlos.";

        return null;
    }

    /// <summary>
    /// Las cabeceras con que se sirve cada fichero subido.
    ///
    /// <list type="bullet">
    /// <item><c>Content-Security-Policy: sandbox</c>: si un fichero se abre como página, corre sin
    /// scripts y en un origen opaco, sin las cookies de la API. A una imagen dentro de un
    /// <c>&lt;img&gt;</c> no le afecta.</item>
    /// <item><c>nosniff</c>: el navegador no adivina el tipo; un «.png» con HTML dentro no se pinta
    /// como página.</item>
    /// <item>Lo ejecutable, como descarga.</item>
    /// </list>
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> ServingHeaders(string fileName)
    {
        // Los PDF, sin sandbox: Chrome bloquea su visor de PDF bajo `sandbox`, y abrir un PDF
        // adjunto dejaría de funcionar. El visor no da acceso al origen de la API, así que no
        // abre la puerta que se cierra aquí.
        if (!Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            yield return new("Content-Security-Policy", "sandbox; default-src 'none'; img-src 'self' data:; media-src 'self'; style-src 'unsafe-inline'");

        yield return new("X-Content-Type-Options", "nosniff");

        if (ExecutableExtensions.Contains(Path.GetExtension(fileName)))
            yield return new("Content-Disposition", "attachment");
    }
}
