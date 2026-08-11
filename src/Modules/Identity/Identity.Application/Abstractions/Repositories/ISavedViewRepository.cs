using Identity.Domain.Entities;

namespace Identity.Application.Abstractions.Repositories;

public interface ISavedViewRepository
{
    Task<IReadOnlyList<SavedView>> GetByUserIdAsync(Guid tenantId, Guid userId, string moduleName, CancellationToken ct = default);
    Task<SavedView?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(SavedView savedView, CancellationToken ct = default);
    Task UpdateAsync(SavedView savedView, CancellationToken ct = default);
    Task DeleteAsync(SavedView savedView, CancellationToken ct = default);
}
