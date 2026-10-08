using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Docs.Infrastructure.Persistence;

/// <summary>
/// Responde el puerto <see cref="IDocumentCatalog"/>.
///
/// Los archivados sí salen: archivar un documento es apartarlo de la lista, no quitarlo de las
/// tareas donde está adjunto. Los borrados no, que es lo que hace el filtro global.
/// </summary>
internal sealed class DocumentCatalog(DocsDbContext context) : IDocumentCatalog
{
    public async Task<IReadOnlyList<DocumentSummary>> GetAsync(
        Guid tenantId, IReadOnlyCollection<Guid> documentIds, CancellationToken ct = default)
    {
        if (documentIds.Count == 0) return [];

        // `EF.Constant` incrusta los identificadores porque Pomelo no traduce una colección como
        // parámetro. Es seguro: son Guid, y la lista es la de los adjuntos de una tarea.
        var ids = documentIds.Distinct().ToArray();

        using var _ = context.IncludeHidden(archived: true);

        return await context.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId && EF.Constant(ids).Contains(d.Id))
            .Select(d => new DocumentSummary(d.Id, d.Title, d.UpdatedAtUtc))
            .ToListAsync(ct);
    }
}
