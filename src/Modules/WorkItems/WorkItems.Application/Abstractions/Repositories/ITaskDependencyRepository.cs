using WorkItems.Domain.Entities;
using WorkItems.Domain.Servicios;

namespace WorkItems.Application.Abstractions.Repositories;

public interface ITaskDependencyRepository
{
  Task<TaskDependency?> GetAsync(Guid tenantId, Guid taskId, Guid dependsOnTaskId, CancellationToken ct = default);

  Task AddAsync(TaskDependency dependency, CancellationToken ct = default);

  void Remove(TaskDependency dependency);

  /// <summary>
  /// Todas las aristas de dependencia de un proyecto, para que el detector de ciclos trabaje
  /// en memoria.
  ///
  /// Se traen de una vez y no saltando de nivel en nivel: un recorrido con una consulta por
  /// salto hace N+1 dentro de la petición, y las dependencias de un proyecto son pocas —del
  /// orden de las tareas que tiene—.
  /// </summary>
  Task<IReadOnlyList<DetectorDeCiclos.Arista>> GetAristasDelProyectoAsync(Guid tenantId, Guid projectId, CancellationToken ct = default);
}
