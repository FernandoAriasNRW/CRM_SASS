using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Entities;

/// <summary>
/// El anclaje de un comentario en línea: qué trozo de qué página se está comentando.
///
/// <b>La conversación no vive aquí.</b> Comentar ya existe en el módulo <c>Comments</c>, con sus
/// reglas de quién edita, quién borra y cómo se responde, y ese módulo dice en su propio código
/// por qué es uno solo para tres entidades: «triplicarla daría tres sitios donde arreglar el mismo
/// fallo». Un cuarto sitio para los documentos sería el mismo error.
///
/// Lo que Docs sí tiene que saber es <b>dónde está pegado</b> el comentario, y eso no lo puede
/// guardar Comments: es una marca sobre un rango de texto dentro de una página concreta. Así que
/// Docs guarda el anclaje y Comments guarda el hilo, con el identificador de la anotación como
/// entidad comentada.
///
/// <b>El texto citado se copia.</b> Podría sacarse del documento leyendo la marca, pero entonces
/// un comentario sobre un párrafo que alguien reescribe se quedaría señalando otra cosa sin decirlo.
/// Guardada la cita, el panel puede enseñar «se comentó sobre esto» aunque el texto ya no exista.
/// </summary>
public sealed class DocumentAnnotation : Entity, ITenantEntity
{
    /// <summary>Lo que se cita se recorta: es una referencia, no una copia del documento.</summary>
    public const int MaxQuoteLength = 300;

    public Guid TenantId { get; private set; }

    public Guid DocumentId { get; private set; }

    /// <summary>La página donde está la marca. Es el grano por el que se listan.</summary>
    public Guid PageId { get; private set; }

    /// <summary>El texto que estaba seleccionado al comentar, tal cual estaba entonces.</summary>
    public string QuotedText { get; private set; } = string.Empty;

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Cuándo se dio por resuelta, o <c>null</c> si sigue abierta.</summary>
    public DateTime? ResolvedAtUtc { get; private set; }

    public Guid? ResolvedBy { get; private set; }

    public bool IsResolved => ResolvedAtUtc is not null;

    private DocumentAnnotation() { }

    public static DocumentAnnotation Create(
        Guid tenantId, Guid documentId, Guid pageId, Guid createdBy, string quotedText)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            PageId = pageId,
            CreatedBy = createdBy,
            QuotedText = Truncate(quotedText),
            CreatedAtUtc = DateTime.UtcNow
        };

    /// <summary>
    /// Da la anotación por resuelta.
    ///
    /// Resolver no es borrar: el hilo se queda y se puede volver a abrir. Un comentario que
    /// desaparece al marcarlo como atendido se lleva por delante el motivo del cambio.
    /// </summary>
    public void Resolve(Guid userId)
    {
        if (IsResolved) return;

        ResolvedAtUtc = DateTime.UtcNow;
        ResolvedBy = userId;
    }

    public void Reopen()
    {
        ResolvedAtUtc = null;
        ResolvedBy = null;
    }

    private static string Truncate(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length > MaxQuoteLength ? trimmed[..MaxQuoteLength] : trimmed;
    }
}
