using Tags.Application.DTOs;

namespace Tags.Application.Abstractions.Queries;

/// <summary>
/// Lecturas del módulo Tags. Devuelve DTOs, nunca entidades: las escrituras van por los
/// repositorios. Los nombres salen tal como están guardados; traducir es cosa del handler.
/// </summary>
public interface ITagQueries
{
    /// <summary>Todas las etiquetas del inquilino, por categoría y nombre.</summary>
    Task<List<TagDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Las categorías que ha creado el inquilino, por nombre.</summary>
    Task<List<TagCategoryDto>> GetCustomCategoriesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
