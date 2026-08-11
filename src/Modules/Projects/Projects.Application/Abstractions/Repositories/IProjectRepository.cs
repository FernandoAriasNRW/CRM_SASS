using Projects.Domain.Entities;

namespace Projects.Application.Abstractions.Repositories;

public interface IProjectRepository
{
  Task<Project?> GetByIdAsync(Guid tenantId, Guid id, bool includeDeleted = false, CancellationToken ct = default);

  Task AddAsync(Project project, CancellationToken ct = default);

  Task UpdateAsync(Project project, CancellationToken ct = default);

  Task<bool> ExistsAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}