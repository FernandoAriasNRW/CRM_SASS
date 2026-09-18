using BuildingBlocks.Application.Abstractions;
using Docs.Domain.Mentions;

namespace Docs.Application.Mentions;

/// <summary>
/// Actualiza las menciones de una página cuando su contenido cambia.
///
/// Vive en un servicio y no dentro del handler de guardar para que se pueda llamar desde los dos
/// sitios que escriben contenido —crear página y actualizarla— sin repetir la lógica en ninguno.
/// </summary>
public sealed class MentionUpdater(IMentionRepository repository)
{
    public async Task UpdateAsync(
        Guid tenantId, Guid documentId, Guid pageId, string? content, CancellationToken ct = default)
    {
        var found = MentionReader.Read(content);

        var mentions = found
            .Select(m => DocumentMention.Create(
                tenantId, documentId, pageId, m.Type, m.EntityId, m.VisibleText))
            .ToList();

        // Se llama siempre, aunque no haya ninguna: una página de la que se han borrado todas las
        // menciones tiene que quedarse sin ellas, y saltarse la llamada dejaría las viejas.
        await repository.ReplacePageMentionsAsync(tenantId, documentId, pageId, mentions, ct);
    }
}
