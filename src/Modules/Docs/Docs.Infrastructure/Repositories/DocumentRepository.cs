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

    public async Task RecordTemplateUsageAsync(Guid tenantId, string key, CancellationToken cancellationToken = default)
    {
        // Se busca sin el filtro de inquilino puesto porque este contador también se toca desde
        // sitios sin petición HTTP; el `tenantId` va explícito en la comparación, así que el
        // aislamiento no depende del filtro.
        var usage = await dbContext.TemplateUsages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Key == key, cancellationToken);

        if (usage is null)
            await dbContext.TemplateUsages.AddAsync(TemplateUsage.First(tenantId, key), cancellationToken);
        else
            usage.Increment();
    }

    public async Task<List<TemplateUsage>> GetTemplateUsagesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.TemplateUsages
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId)
            .OrderByDescending(u => u.Count)
            .ThenByDescending(u => u.LastUsedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DocumentAnnotation>> GetPageAnnotationsAsync(
        Guid pageId, CancellationToken cancellationToken = default)
    {
        return await dbContext.DocumentAnnotations
            .Where(a => a.PageId == pageId)
            .OrderBy(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentAnnotation?> GetAnnotationAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.DocumentAnnotations
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task AddAnnotationAsync(
        DocumentAnnotation annotation, CancellationToken cancellationToken = default)
    {
        await dbContext.DocumentAnnotations.AddAsync(annotation, cancellationToken);
    }

    public Task RemoveAnnotationAsync(
        DocumentAnnotation annotation, CancellationToken cancellationToken = default)
    {
        dbContext.DocumentAnnotations.Remove(annotation);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
