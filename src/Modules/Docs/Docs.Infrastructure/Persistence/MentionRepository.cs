using BuildingBlocks.Application.Abstractions;
using Docs.Application.Mentions;
using Docs.Domain.Mentions;
using Microsoft.EntityFrameworkCore;

namespace Docs.Infrastructure.Persistence;

public sealed class MentionRepository(DocsDbContext context) : IMentionRepository
{
    public async Task ReplacePageMentionsAsync(
        Guid tenantId, Guid documentId, Guid pageId,
        IReadOnlyList<DocumentMention> mentions,
        CancellationToken ct = default)
    {
        // Borrar y volver a escribir, no comparar. Ver el porqué en IMentionRepository: lo
        // que no se detecta al comparar se queda para siempre, y una tarea acabaría enseñando un
        // documento que ya no habla de ella.
        await context.DocumentMentions
            .Where(m => m.TenantId == tenantId && m.PageId == pageId)
            .ExecuteDeleteAsync(ct);

        if (mentions.Count > 0)
            await context.DocumentMentions.AddRangeAsync(mentions, ct);

        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MentioningDocument>> GetMentioningDocumentsAsync(
        Guid tenantId, string type, Guid entityId, CancellationToken ct = default)
    {
        // Se une con la página y el documento para devolver los títulos: quien pregunta va a
        // pintar una lista de enlaces, y sin los títulos tendría que pedir cada uno por separado.
        return await context.DocumentMentions.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.MentionedType == type && m.MentionedEntityId == entityId)
            .Join(context.Pages.AsNoTracking(), m => m.PageId, p => p.Id, (m, p) => new { m, p })
            .Join(context.Documents.AsNoTracking(), x => x.m.DocumentId, d => d.Id, (x, d) => new { x.m, x.p, d })
            // **Se ordena antes de proyectar.** Ordenando después, el criterio es una propiedad
            // del objeto que se acaba de construir y EF no sabe traducir eso: la consulta falla al
            // ejecutarse con «could not be translated», no al compilar.
            .OrderByDescending(x => x.m.DetectedAtUtc)
            .Select(x => new MentioningDocument(
                x.d.Id, x.p.Id, x.d.Title, x.p.Title, x.m.VisibleText, x.m.DetectedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentMention>> GetByPageAsync(
        Guid tenantId, Guid pageId, CancellationToken ct = default)
        => await context.DocumentMentions.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.PageId == pageId)
            .OrderBy(m => m.VisibleText)
            .ToListAsync(ct);
}
