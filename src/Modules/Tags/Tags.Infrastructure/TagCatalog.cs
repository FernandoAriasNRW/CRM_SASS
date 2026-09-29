using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Tags.Infrastructure.Persistence;

namespace Tags.Infrastructure;

/// <summary>Responde el puerto <see cref="ITagCatalog"/>.</summary>
internal sealed class TagCatalog(TagsDbContext dbContext) : ITagCatalog
{
    public async Task<IReadOnlyList<Guid>> FindUnknownAsync(
        Guid tenantId, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default)
    {
        if (tagIds.Count == 0)
            return [];

        var ids = tagIds.Distinct().ToList();

        // Con el inquilino fijado y además en el Where: si alguien pregunta por el id de una
        // etiqueta de otra organización, no tiene que encontrarla ni con el filtro apagado.
        //
        // Se traen todos los ids de la organización y se compara en memoria: `ids.Contains(t.Id)`
        // con una lista como parámetro no lo traduce Pomelo sin activar las colecciones
        // primitivas, y una organización tiene decenas de etiquetas, no miles.
        using var _ = dbContext.AsTenant(tenantId);
        var known = (await dbContext.Tags
                .Where(t => t.TenantId == tenantId)
                .Select(t => t.Id)
                .ToListAsync(ct))
            .ToHashSet();

        return ids.Where(id => !known.Contains(id)).ToList();
    }
}
