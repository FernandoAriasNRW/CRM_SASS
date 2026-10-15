using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using Docs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Docs.Infrastructure.Repositories;

public class DocumentRepository(TimeProvider timeProvider, DocsDbContext dbContext) : IDocumentRepository
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

    public async Task<List<Document>> GetListAsync(Guid tenantId, BuildingBlocks.Application.ViewScope scope, CancellationToken cancellationToken = default)
    {
        // La papelera y el archivo quedan fuera de lo que el filtro global deja ver, así que hay
        // que abrir el alcance antes de consultar. Se cierra al terminar.
        using var _ = dbContext.IncludeHidden(
            deleted: scope.Is(BuildingBlocks.Application.ViewFilters.Trash),
            archived: scope.Is(BuildingBlocks.Application.ViewFilters.Archived));

        var query = dbContext.Documents.AsNoTracking().Where(d => d.TenantId == tenantId);
        var me = scope.UserId;

        if (scope.Is(BuildingBlocks.Application.ViewFilters.Mine) && me.HasValue)
            query = query.Where(d => d.OwnerId == me.Value);
        else if (scope.Is(BuildingBlocks.Application.ViewFilters.SharedWithMe))
        {
            var shared = scope.SharedWithMe.ToArray();
            query = query.Where(d => EF.Constant(shared).Contains(d.Id));
        }
        else if (scope.Is(BuildingBlocks.Application.ViewFilters.Archived))
            query = query.Where(d => d.ArchivedAtUtc != null);
        else if (scope.Is(BuildingBlocks.Application.ViewFilters.Trash))
            query = query.Where(d => d.IsDeleted);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<Document?> GetIncludingHiddenAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        using var _ = dbContext.IncludeHidden(deleted: true, archived: true);
        return await dbContext.Documents.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == id, cancellationToken);
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
            await dbContext.TemplateUsages.AddAsync(TemplateUsage.First(timeProvider.GetUtcNow().UtcDateTime, tenantId, key), cancellationToken);
        else
            usage.Increment(timeProvider.GetUtcNow().UtcDateTime);
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
