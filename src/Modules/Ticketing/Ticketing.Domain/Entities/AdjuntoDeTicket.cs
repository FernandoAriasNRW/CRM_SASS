using BuildingBlocks.Domain.Primitives;

namespace Ticketing.Domain.Entities;

/// <summary>
/// Una imagen o un vídeo adjunto a un ticket: la captura del error, la grabación de la pantalla.
///
/// El fichero vive en el almacenamiento de la aplicación; aquí queda dónde está y qué es.
/// <see cref="SubidoPor"/> vacío significa que llegó desde fuera con el ticket, sin usuario.
/// </summary>
public sealed class AdjuntoDeTicket : Entity, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid TicketId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string TipoDeContenido { get; private set; } = string.Empty;
    public long Tamano { get; private set; }
    public Guid? SubidoPor { get; private set; }
    public DateTime SubidoUtc { get; private set; }

    private AdjuntoDeTicket() { }

    public static AdjuntoDeTicket Crear(
        Ticket ticket, string nombre, string url, string tipoDeContenido, long tamano, Guid? subidoPor, DateTime ahoraUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = ticket.TenantId,
            TicketId = ticket.Id,
            Nombre = nombre.Length > 255 ? nombre[..255] : nombre,
            Url = url,
            TipoDeContenido = tipoDeContenido,
            Tamano = tamano,
            SubidoPor = subidoPor,
            SubidoUtc = ahoraUtc
        };
}

/// <summary>
/// Qué se admite como adjunto. En el dominio y no en el endpoint para que el formulario de fuera y
/// la ficha de la aplicación no acaben aceptando cosas distintas.
/// </summary>
public static class ReglasDeAdjuntos
{
    public const int MaximoDeFicheros = 10;
    public const long MaximoPorFichero = 50L * 1024 * 1024;

    /// <summary>Lo más que puede pesar una petición con adjuntos: todos al máximo, y margen para el resto.</summary>
    public const long MaximoDeLaPeticion = MaximoDeFicheros * MaximoPorFichero + 1024 * 1024;

    /// <summary>
    /// Sólo imágenes y vídeos. Se mira el tipo declarado y además la extensión: el tipo lo pone
    /// quien envía, y un «image/png» llamado «factura.exe» no es una captura.
    /// </summary>
    private static readonly HashSet<string> Extensiones = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".heic", ".bmp",
        ".mp4", ".mov", ".webm", ".m4v", ".avi", ".mkv"
    };

    /// <summary>El motivo por el que no se admite, o <c>null</c> si se admite.</summary>
    public static string? Rechazo(string nombre, string tipoDeContenido, long tamano)
    {
        var esMedio = tipoDeContenido.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                      || tipoDeContenido.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

        if (!esMedio || !Extensiones.Contains(Path.GetExtension(nombre)))
            return $"«{nombre}» no es una imagen ni un vídeo";

        if (tamano <= 0)
            return $"«{nombre}» está vacío";

        if (tamano > MaximoPorFichero)
            return $"«{nombre}» supera los {MaximoPorFichero / (1024 * 1024)} MB";

        return null;
    }
}
