using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.DTOs;

namespace WorkItems.Application.Queries;

public sealed record GetTasksQuery(
    Guid TenantId,
    Guid? ProjectId,
    Guid? AssigneeId,
    string? Status,
    string? Priority,
    string? Filter,
    Guid? UserId,
    PaginationRequest Pagination
) : IQuery<PagedResult<TaskDto>>;
