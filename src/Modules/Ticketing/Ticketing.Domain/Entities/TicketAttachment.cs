using BuildingBlocks.Domain.Primitives;

namespace Ticketing.Domain.Entities;

/// <summary>
/// Una imagen o un vídeo adjunto a un ticket: la captura del error, la grabación de la pantalla.
///
/// El fichero vive en el almacenamiento de la aplicación; aquí queda dónde está y qué es.
/// <see cref="UploadedBy"/> vacío significa que llegó desde fuera con el ticket, sin usuario.
/// </summary>
public sealed class TicketAttachment : Entity, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid TicketId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public Guid? UploadedBy { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    private TicketAttachment() { }

    public static TicketAttachment Create(
        Ticket ticket, string name, string url, string contentType, long size, Guid? uploadedBy, DateTime nowUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = ticket.TenantId,
            TicketId = ticket.Id,
            Name = name.Length > 255 ? name[..255] : name,
            Url = url,
            ContentType = contentType,
            Size = size,
            UploadedBy = uploadedBy,
            UploadedAtUtc = nowUtc
        };
}
