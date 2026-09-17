using Microsoft.EntityFrameworkCore;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Domain.Entities;
using WorkItems.Infrastructure.Persistence;

namespace WorkItems.Infrastructure.Repositories;

public sealed class EfTaskRepository(WorkItemsDbContext context) : ITaskRepository
{
  public async Task<WorkTask?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
      => await context.Tasks.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

  public async Task<WorkTask?> GetIncludingHiddenAsync(Guid tenantId, Guid id, CancellationToken ct = default)
  {
    // El ámbito se cierra al salir del `using`: si se quedara abierto, las consultas siguientes
    // de esta misma petición empezarían a devolver tareas borradas sin que nadie lo pidiera.
    // Ojo: no vale `IgnoreQueryFilters()`, que apagaría también el aislamiento por inquilino.
    using var _ = context.IncludeHidden(deleted: true, archived: true);

    return await context.Tasks.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);
  }

  public async Task AddAsync(WorkTask task, CancellationToken ct = default)
      => await context.Tasks.AddAsync(task, ct);

  public Task UpdateAsync(WorkTask task, CancellationToken ct = default)
  {
    context.Tasks.Update(task);
    return Task.CompletedTask;
  }

  public async Task<int> CountSubtasksAsync(Guid tenantId, Guid parentTaskId, CancellationToken ct = default)
      => await context.Tasks.CountAsync(t => t.TenantId == tenantId && t.ParentTaskId == parentTaskId, ct);
}