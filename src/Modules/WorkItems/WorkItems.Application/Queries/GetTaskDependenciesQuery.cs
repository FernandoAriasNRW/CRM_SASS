using BuildingBlocks.Application.Abstractions;
using WorkItems.Application.DTOs;

namespace WorkItems.Application.Queries;

public sealed record GetTaskDependenciesQuery(
    Guid TenantId,
    Guid TaskId
) : IQuery<TaskDependenciesDto>;
