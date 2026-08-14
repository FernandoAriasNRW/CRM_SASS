using BuildingBlocks.Domain;
using WorkItems.Application.DTOs;

namespace WorkItems.Application.Abstractions.Queries;

public interface ITaskQueries
{
  Task<PagedResult<TaskDto>> GetByTenantAsync(
      Guid tenantId,
      Guid? projectId,
      Guid? assigneeId,
      string? status,
      int page,
      int pageSize,
      CancellationToken ct = default);

  /// <param name="parentTaskId">Si viene, devuelve las subtareas de esa tarea.</param>
  /// <param name="incluirSubtareas">
  /// Sin <paramref name="parentTaskId"/>, por defecto se devuelven sólo las tareas de primer
  /// nivel. Con <c>true</c> se mezclan también las subtareas.
  /// </param>
  Task<PagedResult<TaskDto>> GetByTenantWithPaginationAsync(Guid tenantId, Guid? projectId, Guid? assigneeId, string? status, string? priority, string? filter, Guid? userId, Guid? parentTaskId, bool incluirSubtareas, PaginationRequest pagination, CancellationToken ct = default);

  Task<TaskDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

  /// <summary>
  /// Las dependencias de una tarea, en las dos direcciones: las que la bloquean y las que ella
  /// bloquea. Se devuelven juntas porque la interfaz las muestra juntas y separarlas serían dos
  /// viajes para pintar un solo panel.
  /// </summary>
  Task<TaskDependenciesDto> GetDependenciesAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);

  /// <summary>
  /// Todas las dependencias del inquilino, como aristas sueltas.
  ///
  /// El diagrama de Gantt necesita el grafo entero para dibujar las flechas, y pedirlo tarea por
  /// tarea serían tantas peticiones como filas: con veinticinco tareas en pantalla, veinticinco
  /// viajes para pintar una vista. Son pocas filas —una dependencia por par de tareas, no un
  /// registro por evento—, así que caben en una consulta.
  /// </summary>
  Task<IReadOnlyList<TaskDependencyEdgeDto>> GetDependencyGraphAsync(Guid tenantId, CancellationToken ct = default);

  /// <summary>Los puntos de la checklist de una tarea, ya ordenados por su posición.</summary>
  Task<IReadOnlyList<ChecklistItemDto>> GetChecklistAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);
}