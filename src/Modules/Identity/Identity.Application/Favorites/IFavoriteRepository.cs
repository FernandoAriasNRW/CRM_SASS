using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Domain.Entities;

namespace Identity.Application.Favorites;

public interface IFavoriteRepository
{
    /// <summary>Los identificadores que esta persona marcó de un tipo. Es lo que usa el filtro.</summary>
    Task<IReadOnlyList<Guid>> GetIdsAsync(Guid tenantId, Guid userId, string entityType, CancellationToken ct);

    Task<Favorite?> FindAsync(Guid tenantId, Guid userId, string entityType, Guid entityId, CancellationToken ct);
    Task<int> CountAsync(Guid tenantId, Guid userId, string entityType, CancellationToken ct);

    Task AddAsync(Favorite favorite, CancellationToken ct);
    void Remove(Favorite favorite);
    Task SaveChangesAsync(CancellationToken ct);
}
