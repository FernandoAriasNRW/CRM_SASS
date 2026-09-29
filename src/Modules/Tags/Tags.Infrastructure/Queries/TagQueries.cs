using Microsoft.EntityFrameworkCore;
using Tags.Application.Abstractions.Queries;
using Tags.Application.DTOs;
using Tags.Infrastructure.Persistence;

namespace Tags.Infrastructure.Queries;

// El filtro global ya limita al inquilino de la petición; el Where explícito es por si esto se
// llama algún día fuera de una petición, donde el filtro no tiene inquilino con el que comparar y
// la tabla se ve vacía.
internal sealed class TagQueries(TagsDbContext dbContext) : ITagQueries
{
    // CategoryLabel sale con el valor guardado y CanManage en false; los rellena el handler.
    public Task<List<TagDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => dbContext.Tags
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.Category).ThenBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.ColorHex, t.Category, t.Category, t.ExternalReferenceId, t.BuiltInKey, t.CreatedBy, false))
            .ToListAsync(cancellationToken);

    public Task<List<TagCategoryDto>> GetCustomCategoriesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => dbContext.CustomTagCategories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .Select(c => new TagCategoryDto(c.Id, c.Name, c.Name, true, false))
            .ToListAsync(cancellationToken);
}
