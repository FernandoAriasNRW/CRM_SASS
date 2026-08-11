using WorkItems.Domain.Entities;

namespace WorkItems.Application.Abstractions.Repositories;

public interface ITaskRepository
{
  Task<WorkTask?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

  Task AddAsync(WorkTask task, CancellationToken ct = default);

  Task UpdateAsync(WorkTask task, CancellationToken ct = default);
}