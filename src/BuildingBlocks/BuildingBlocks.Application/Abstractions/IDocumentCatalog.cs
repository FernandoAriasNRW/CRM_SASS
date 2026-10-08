namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Qué documentos existen en una organización y cómo se llaman, para los módulos que los enlazan.
///
/// Una tarea guarda los documentos que tiene adjuntos por su identificador: comprobar que existen
/// al adjuntarlos y saber su título al enseñarlos es cosa de Docs, que es quien los tiene. Es un
/// puerto por lo mismo que <see cref="ITagCatalog"/>. Lo implementa Docs.
/// </summary>
public interface IDocumentCatalog
{
    /// <summary>
    /// Los documentos de la lista que existen en la organización, con su título. Los borrados no
    /// salen: un adjunto a un documento que ya no está no tiene nada que abrir.
    /// </summary>
    Task<IReadOnlyList<DocumentSummary>> GetAsync(Guid tenantId, IReadOnlyCollection<Guid> documentIds, CancellationToken ct = default);
}

/// <summary>Lo justo de un documento para enlazarlo y pintarlo.</summary>
public sealed record DocumentSummary(Guid Id, string Title, DateTime UpdatedAtUtc);
