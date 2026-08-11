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
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
