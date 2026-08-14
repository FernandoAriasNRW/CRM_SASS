using BuildingBlocks.Application.Abstractions;
using WorkItems.Application.DTOs;

namespace WorkItems.Application.Queries;

public sealed record GetTaskDependenciesQuery(
    Guid TenantId,
    Guid TaskId
) : IQuery<TaskDependenciesDto>;

/// <summary>
/// El grafo entero de dependencias del inquilino. Lo pide el Gantt para dibujar las flechas sin
/// una petición por tarea.
/// </summary>
public sealed record GetDependencyGraphQuery(
    Guid TenantId
) : IQuery<IReadOnlyList<TaskDependencyEdgeDto>>;
