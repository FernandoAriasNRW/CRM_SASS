using Tags.Domain.Entities;

namespace Tags.Application.Abstractions.Repositories;

/// <summary>Las categorías personalizadas; las predefinidas viven en código (<c>TagCategory</c>).</summary>
public interface ITagCategoryRepository
{
    Task AddAsync(CustomTagCategory category, CancellationToken cancellationToken = default);

    Task<CustomTagCategory?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Si ya hay una con ese nombre. <paramref name="exceptId"/> es la propia al renombrar.</summary>
    Task<bool> ExistsAsync(Guid tenantId, string name, Guid? exceptId = null, CancellationToken cancellationToken = default);

    /// <summary>Cuántas etiquetas están en la categoría de ese nombre.</summary>
    Task<int> CountTagsAsync(Guid tenantId, string categoryName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renombra la categoría y mueve sus etiquetas al nombre nuevo, en una transacción: las
    /// etiquetas la referencian por nombre, y a medias quedarían apuntando a una que ya no existe.
    /// </summary>
    Task RenameAsync(CustomTagCategory category, string newName, CancellationToken cancellationToken = default);

    Task RemoveAsync(CustomTagCategory category, CancellationToken cancellationToken = default);
}
