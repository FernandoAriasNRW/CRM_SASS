using Projects.Domain.Entities;

namespace Projects.Application.Abstractions.Repositories;

public interface ISpaceRepository
{
    Task<Space?> GetByIdAsync(Guid tenantId, Guid spaceId, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<Space>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task AddAsync(Space space, CancellationToken cancellationToken = default);
    Task UpdateAsync(Space space, CancellationToken cancellationToken = default);
}
