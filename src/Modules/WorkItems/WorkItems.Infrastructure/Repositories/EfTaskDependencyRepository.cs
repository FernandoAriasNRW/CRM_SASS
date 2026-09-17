using Microsoft.EntityFrameworkCore;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Domain.Entities;
using WorkItems.Domain.Services;
using WorkItems.Infrastructure.Persistence;

namespace WorkItems.Infrastructure.Repositories;

public sealed class EfTaskDependencyRepository(WorkItemsDbContext context) : ITaskDependencyRepository
{
  public async Task<TaskDependency?> GetAsync(Guid tenantId, Guid taskId, Guid dependsOnTaskId, CancellationToken ct = default)
      => await context.TaskDependencies.FirstOrDefaultAsync(
          d => d.TenantId == tenantId && d.TaskId == taskId && d.DependsOnTaskId == dependsOnTaskId, ct);

  public async Task AddAsync(TaskDependency dependency, CancellationToken ct = default)
      => await context.TaskDependencies.AddAsync(dependency, ct);

  public void Remove(TaskDependency dependency)
      => context.TaskDependencies.Remove(dependency);

  public async Task<IReadOnlyList<CycleDetector.Edge>> GetProjectEdgesAsync(
      Guid tenantId, Guid projectId, CancellationToken ct = default)
  {
    // Las aristas cuyas dos puntas están en el proyecto. Se filtra por las tareas del proyecto
    // en lugar de guardar el proyecto en la arista: duplicarlo obligaría a mantenerlo al día
    // cuando una tarea se mueve de proyecto.
    var projectTasks = context.Tasks
        .Where(t => t.TenantId == tenantId && t.ProjectId == projectId)
        .Select(t => t.Id);

    return await context.TaskDependencies
        .AsNoTracking()
        .Where(d => d.TenantId == tenantId
                    && projectTasks.Contains(d.TaskId)
                    && projectTasks.Contains(d.DependsOnTaskId))
        .Select(d => new CycleDetector.Edge(d.TaskId, d.DependsOnTaskId))
        .ToListAsync(ct);
  }
}
