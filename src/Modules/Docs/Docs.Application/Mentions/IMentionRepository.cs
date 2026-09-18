using BuildingBlocks.Application.Abstractions;
using Docs.Domain.Mentions;

namespace Docs.Application.Mentions;

/// <summary>
/// Guarda las menciones de una página a partir de su contenido.
///
/// <b>Se reescriben enteras cada vez que se guarda la página</b>, en vez de ir añadiendo y
/// quitando. Es lo que hace que borrar una mención del texto la borre de verdad: con un
/// diferencial habría que detectar la desaparición, y lo que no se detecta se queda para siempre
/// —una tarea enseñando un documento que ya no habla de ella—.
///
/// El coste es un borrado y una inserción por guardado, sobre una tabla indexada por página y con
/// pocas filas por página. A cambio, el estado no puede divergir del texto.
/// </summary>
public interface IMentionRepository
{
    /// <summary>Reemplaza las menciones de una página por las que trae el contenido.</summary>
    Task ReplacePageMentionsAsync(
        Guid tenantId, Guid documentId, Guid pageId,
        IReadOnlyList<DocumentMention> mentions,
        CancellationToken ct = default);

    /// <summary>Los documentos que mencionan una entidad. Es lo que responde el puerto.</summary>
    Task<IReadOnlyList<MentioningDocument>> GetMentioningDocumentsAsync(
        Guid tenantId, string type, Guid entityId, CancellationToken ct = default);

    /// <summary>Lo que menciona una página, para pintarlo dentro del propio documento.</summary>
    Task<IReadOnlyList<DocumentMention>> GetByPageAsync(
        Guid tenantId, Guid pageId, CancellationToken ct = default);
}
