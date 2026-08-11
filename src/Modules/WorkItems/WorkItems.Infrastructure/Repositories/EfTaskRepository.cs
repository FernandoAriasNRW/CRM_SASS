using Microsoft.EntityFrameworkCore;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Domain.Entities;
using WorkItems.Infrastructure.Persistence;

namespace WorkItems.Infrastructure.Repositories;

public sealed class EfTaskRepository(WorkItemsDbContext context) : ITaskRepository
{
  public async Task<WorkTask?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
      => await context.Tasks.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

  public async Task AddAsync(WorkTask task, CancellationToken ct = default)
      => await context.Tasks.AddAsync(task, ct);

  public Task UpdateAsync(WorkTask task, CancellationToken ct = default)
  {
    context.Tasks.Update(task);
    return Task.CompletedTask;
  }
}