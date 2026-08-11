using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using Docs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Docs.Infrastructure.Repositories;

public class DocumentRepository(DocsDbContext dbContext) : IDocumentRepository
{
    public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        await dbContext.Documents.AddAsync(document, cancellationToken);
    }

    public async Task<List<Document>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Documents
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Documents
            .Include(d => d.Pages)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Page?> GetPageByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Page>()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task AddPageAsync(Page page, CancellationToken cancellationToken = default)
    {
        await dbContext.Pages.AddAsync(page, cancellationToken);
    }

    public async Task<List<Page>> GetPagesByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Pages
            .Where(p => p.DocumentId == documentId)
            .OrderBy(p => p.Order)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
