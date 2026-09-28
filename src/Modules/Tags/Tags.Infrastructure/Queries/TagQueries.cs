using Microsoft.EntityFrameworkCore;
using Tags.Application.Abstractions.Queries;
using Tags.Application.DTOs;
using Tags.Infrastructure.Persistence;

namespace Tags.Infrastructure.Queries;

internal sealed class TagQueries(TagsDbContext dbContext) : ITagQueries
{
    // El filtro global ya limita al inquilino de la petición; el Where explícito es por si esto
    // se llama algún día fuera de una petición, donde el filtro no tiene inquilino con el que
    // comparar y la tabla se ve vacía.
    public Task<List<TagDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => dbContext.Tags
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.ColorHex, t.Category, t.ExternalReferenceId))
            .ToListAsync(cancellationToken);
}
