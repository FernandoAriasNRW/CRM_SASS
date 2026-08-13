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
}