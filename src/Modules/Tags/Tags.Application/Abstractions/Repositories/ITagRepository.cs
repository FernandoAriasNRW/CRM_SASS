using Tags.Domain.Entities;

namespace Tags.Application.Abstractions.Repositories;

public interface ITagRepository
{
    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Si el inquilino ya tiene una etiqueta con ese nombre en esa categoría. La base lo impide con
    /// un índice único (<c>TenantId</c>, <c>Category</c>, <c>Name</c>), pero chocar contra él da un
    /// 500; preguntando antes se responde con un error que el cliente entiende.
    /// </summary>
    Task<bool> ExistsByNameAsync(Guid tenantId, string category, string name, CancellationToken cancellationToken = default);
}
