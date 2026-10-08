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
    /// <summary>
    /// La entrada del panel de navegación ya resuelta: filtro, quién pregunta y las listas que
    /// sólo se pueden saber fuera del módulo. Ver <c>ViewScope</c>.
    /// </summary>
    BuildingBlocks.Application.ViewScope? ViewScope,
    PaginationRequest Pagination,
    Guid? ParentTaskId = null,
    bool IncludeSubtasks = false,
    /// <summary>
    /// El tablero de un equipo: sólo las tareas que lleva alguno de estos miembros. <c>null</c> es
    /// no filtrar; una lista vacía —un equipo sin nadie— no devuelve nada.
    /// </summary>
    IReadOnlyList<Guid>? TeamMemberIds = null
) : IQuery<PagedResult<TaskDto>>;
