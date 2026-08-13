using WorkItems.Domain.Entities;

namespace WorkItems.Application.Abstractions.Repositories;

public interface ITaskRepository
{
  Task<WorkTask?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

  Task AddAsync(WorkTask task, CancellationToken ct = default);

  Task UpdateAsync(WorkTask task, CancellationToken ct = default);

  /// <summary>
  /// Cuántas subtareas tiene una tarea.
  ///
  /// Hace falta para aplicar las reglas de anidamiento: el agregado no puede saber si otras
  /// filas cuelgan de él, y una tarea con subtareas no puede convertirse en subtarea.
  /// </summary>
  Task<int> CountSubtasksAsync(Guid tenantId, Guid parentTaskId, CancellationToken ct = default);
}