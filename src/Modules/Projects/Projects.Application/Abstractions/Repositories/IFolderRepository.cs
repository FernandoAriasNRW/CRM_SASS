using Projects.Domain.Entities;

namespace Projects.Application.Abstractions.Repositories;

public interface IFolderRepository
{
    Task<Folder?> GetByIdAsync(Guid tenantId, Guid folderId, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<Folder>> GetAllAsync(Guid tenantId, Guid spaceId, CancellationToken cancellationToken = default);
    Task AddAsync(Folder folder, CancellationToken cancellationToken = default);
    Task UpdateAsync(Folder folder, CancellationToken cancellationToken = default);
}
