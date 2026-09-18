using Docs.Domain.Entities;

namespace Docs.Application.Abstractions.Repositories;

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken cancellationToken = default);
    Task<List<Document>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Page?> GetPageByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddPageAsync(Page page, CancellationToken cancellationToken = default);
    Task<List<Page>> GetPagesByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    /// <summary>Suma uno al contador de la plantilla, creando la fila si es la primera vez.</summary>
    Task RecordTemplateUsageAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);

    /// <summary>Cuánto se ha usado cada plantilla en este inquilino.</summary>
    Task<List<TemplateUsage>> GetTemplateUsagesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Las anotaciones pegadas a una página, resueltas incluidas.</summary>
    Task<List<DocumentAnnotation>> GetPageAnnotationsAsync(Guid pageId, CancellationToken cancellationToken = default);

    Task<DocumentAnnotation?> GetAnnotationAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAnnotationAsync(DocumentAnnotation annotation, CancellationToken cancellationToken = default);

    Task RemoveAnnotationAsync(DocumentAnnotation annotation, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
