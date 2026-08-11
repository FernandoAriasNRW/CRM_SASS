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

  Task<PagedResult<TaskDto>> GetByTenantWithPaginationAsync(Guid tenantId, Guid? projectId, Guid? assigneeId, string? status, string? filter, Guid? userId, PaginationRequest pagination, CancellationToken ct = default);

  Task<TaskDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}