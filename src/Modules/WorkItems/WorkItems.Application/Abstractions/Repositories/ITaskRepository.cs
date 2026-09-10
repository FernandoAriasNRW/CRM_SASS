using WorkItems.Domain.Entities;

namespace WorkItems.Application.Abstractions.Repositories;

public interface ITaskRepository
{
  Task<WorkTask?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

  /// <summary>
  /// Busca la tarea aunque esté archivada o en la papelera.
  ///
  /// Existe porque el filtro global esconde las dos cosas y restaurar algo exige poder
  /// encontrarlo primero. Es la excepción, no la norma: quien la use debe tener una razón para
  /// mirar donde nadie mira.
  /// </summary>
  Task<WorkTask?> GetIncluyendoOcultosAsync(Guid tenantId, Guid id, CancellationToken ct = default);

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