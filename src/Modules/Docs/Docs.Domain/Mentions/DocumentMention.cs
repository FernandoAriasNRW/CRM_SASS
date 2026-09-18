using System.Text.RegularExpressions;
using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Mentions;

/// <summary>
/// Una mención dentro de un documento: «esta página nombra esta tarea».
///
/// <b>Es el diferencial del producto, y por eso existe esta tabla en vez de dejarlo en el HTML.</b>
/// El plan lo dice: «mencionar un ticket dentro de un documento y que el ticket muestre el
/// documento es algo que ClickUp hace a medias». La primera mitad —escribir <c>#tarea</c> y que
/// quede un enlace— se resuelve con una extensión del editor. La segunda —que la tarea sepa qué
/// documentos hablan de ella— <b>no se puede resolver mirando el documento</b>: habría que abrir
/// todos los documentos del inquilino y buscar dentro. Por eso la relación se guarda al derecho,
/// desde la página, y se consulta al revés.
///
/// <b>Las menciones se derivan del contenido, no se mandan aparte.</b> Al guardar una página se
/// vuelven a extraer de lo que se ha guardado. Aceptar una lista de menciones del cliente
/// permitiría que el documento dijera una cosa y la tabla otra: alguien borra la mención del texto
/// y la tarea sigue enseñando el documento para siempre.
/// </summary>
public sealed class DocumentMention : Entity, ITenantEntity
{
    public Guid TenantId { get; private set; }

    /// <summary>La página que menciona. Es el grano del que se borra y se vuelve a escribir.</summary>
    public Guid PageId { get; private set; }

    /// <summary>El documento al que pertenece la página, para poder enlazarlo sin otra consulta.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Qué se menciona. Uno de <see cref="EntityTypes"/>, más «Persona».</summary>
    public string MentionedType { get; private set; } = string.Empty;

    public Guid MentionedEntityId { get; private set; }

    /// <summary>
    /// Cómo se ve la mención en el texto, tal como estaba al guardar.
    ///
    /// Se guarda una copia a propósito, aunque el nombre real esté en el otro módulo: sirve para
    /// pintar la lista de «mencionado en» sin ir a buscar cada nombre, y para que una mención a
    /// algo que luego se borró siga diciendo a qué se refería en vez de quedar como un hueco.
    /// </summary>
    public string VisibleText { get; private set; } = string.Empty;

    public DateTime DetectedAtUtc { get; private set; }

    private DocumentMention() { }

    public static DocumentMention Create(
        Guid tenantId, Guid documentId, Guid pageId, string type, Guid entityId, string visibleText)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            PageId = pageId,
            MentionedType = type,
            MentionedEntityId = entityId,
            VisibleText = Truncate(visibleText),
            DetectedAtUtc = DateTime.UtcNow
        };

    /// <summary>El texto visible se recorta: es una etiqueta, no el contenido de la página.</summary>
    private static string Truncate(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length > 200 ? trimmed[..200] : trimmed;
    }
}
