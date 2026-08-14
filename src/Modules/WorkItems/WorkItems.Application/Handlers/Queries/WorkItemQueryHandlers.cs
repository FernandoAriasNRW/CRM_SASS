using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions.Queries;
using WorkItems.Application.DTOs;
using WorkItems.Application.Queries;

namespace WorkItems.Application.Handlers.Queries;

public sealed class GetTasksQueryHandler(ITaskQueries queries)
    : IQueryHandler<GetTasksQuery, PagedResult<TaskDto>>
{
  public async Task<Result<PagedResult<TaskDto>>> Handle(GetTasksQuery request, CancellationToken cancellationToken)
  {
    var result = await queries.GetByTenantWithPaginationAsync(
        request.TenantId, request.ProjectId, request.AssigneeId, request.Status,
        request.Priority, request.Filter, request.UserId,
        request.ParentTaskId, request.IncluirSubtareas,
        request.Pagination, cancellationToken);

    return Result<PagedResult<TaskDto>>.Success(result);
  }
}

public sealed class GetTaskDependenciesQueryHandler(ITaskQueries queries)
    : IQueryHandler<GetTaskDependenciesQuery, TaskDependenciesDto>
{
  public async Task<Result<TaskDependenciesDto>> Handle(GetTaskDependenciesQuery request, CancellationToken cancellationToken)
      => Result<TaskDependenciesDto>.Success(
          await queries.GetDependenciesAsync(request.TenantId, request.TaskId, cancellationToken));
}

public sealed class GetDependencyGraphQueryHandler(ITaskQueries queries)
    : IQueryHandler<GetDependencyGraphQuery, IReadOnlyList<TaskDependencyEdgeDto>>
{
  public async Task<Result<IReadOnlyList<TaskDependencyEdgeDto>>> Handle(GetDependencyGraphQuery request, CancellationToken cancellationToken)
      => Result<IReadOnlyList<TaskDependencyEdgeDto>>.Success(
          await queries.GetDependencyGraphAsync(request.TenantId, cancellationToken));
}

public sealed class GetChecklistQueryHandler(ITaskQueries queries)
    : IQueryHandler<GetChecklistQuery, IReadOnlyList<ChecklistItemDto>>
{
  public async Task<Result<IReadOnlyList<ChecklistItemDto>>> Handle(GetChecklistQuery request, CancellationToken cancellationToken)
      => Result<IReadOnlyList<ChecklistItemDto>>.Success(
          await queries.GetChecklistAsync(request.TenantId, request.TaskId, cancellationToken));
}

public sealed class GetTaskByIdQueryHandler(ITaskQueries queries)
    : IQueryHandler<GetTaskByIdQuery, TaskDto?>
{
  public async Task<Result<TaskDto?>> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
  {
    var task = await queries.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    return Result<TaskDto?>.Success(task);
  }
}
