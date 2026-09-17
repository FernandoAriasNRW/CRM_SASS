using BuildingBlocks.Domain.Primitives;

namespace Ticketing.Domain.Entities;

/// <summary>
/// Qué se admite como adjunto. En el dominio y no en el endpoint para que el formulario de fuera y
/// la ficha de la aplicación no acaben aceptando cosas distintas.
/// </summary>
public static class AttachmentRules
{
    public const int MaxFiles = 10;
    public const long MaxBytesPerFile = 50L * 1024 * 1024;

    /// <summary>Lo más que puede pesar una petición con adjuntos: todos al máximo, y margen para el resto.</summary>
    public const long MaxRequestBytes = MaxFiles * MaxBytesPerFile + 1024 * 1024;

    /// <summary>
    /// Sólo imágenes y vídeos. Se mira el tipo declarado y además la extensión: el tipo lo pone
    /// quien envía, y un «image/png» llamado «factura.exe» no es una captura.
    /// </summary>
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".heic", ".bmp",
        ".mp4", ".mov", ".webm", ".m4v", ".avi", ".mkv"
    };

    /// <summary>El motivo por el que no se admite, o <c>null</c> si se admite.</summary>
    public static string? RejectionReason(string name, string contentType, long size)
    {
        var isMedia = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                      || contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

        if (!isMedia || !Extensions.Contains(Path.GetExtension(name)))
            return $"«{name}» no es una imagen ni un vídeo";

        if (size <= 0)
            return $"«{name}» está vacío";

        if (size > MaxBytesPerFile)
            return $"«{name}» supera los {MaxBytesPerFile / (1024 * 1024)} MB";

        return null;
    }
}
