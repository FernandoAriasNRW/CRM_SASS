using Tags.Application.DTOs;

namespace Tags.Application.Abstractions.Queries;

/// <summary>
/// Lecturas del módulo Tags. Devuelve DTOs, nunca entidades: las escrituras van por
/// <see cref="Repositories.ITagRepository"/>.
/// </summary>
public interface ITagQueries
{
    /// <summary>Todas las etiquetas del inquilino, por nombre.</summary>
    Task<List<TagDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
