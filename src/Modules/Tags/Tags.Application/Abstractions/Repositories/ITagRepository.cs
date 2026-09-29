using Tags.Domain.Entities;

namespace Tags.Application.Abstractions.Repositories;

public interface ITagRepository
{
    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);

    Task<Tag?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    Task RemoveAsync(Tag tag, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Si el inquilino ya tiene una etiqueta con ese nombre en esa categoría. La base lo impide con
    /// un índice único (<c>TenantId</c>, <c>Category</c>, <c>Name</c>), pero chocar contra él da un
    /// 500; preguntando antes se responde con un error que el cliente entiende.
    /// </summary>
    Task<bool> ExistsByNameAsync(Guid tenantId, string category, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Como <see cref="ExistsByNameAsync"/>, y además si choca con una predefinida que la
    /// organización tenga en la otra lengua: «VIP client» al lado de «Cliente VIP» sería la misma
    /// etiqueta dos veces, y el índice no lo vería porque se guarda en español. Al editar,
    /// <paramref name="exceptTag"/> es la propia etiqueta, que no choca consigo misma.
    /// </summary>
    Task<bool> NameIsTakenAsync(Guid tenantId, string category, string name, Tag? exceptTag, CancellationToken cancellationToken = default);
}
