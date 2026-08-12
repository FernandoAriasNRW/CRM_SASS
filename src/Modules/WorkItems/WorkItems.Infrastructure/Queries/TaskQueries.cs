using System.Linq.Expressions;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using WorkItems.Application.Abstractions.Queries;
using WorkItems.Application.DTOs;
using WorkItems.Domain.Entities;
using WorkItems.Domain.ValueObjects;
using WorkItems.Infrastructure.Persistence;

namespace WorkItems.Infrastructure.Queries;

public sealed class TaskQueries(WorkItemsDbContext context) : ITaskQueries
{
  /// <summary>
  /// Posición de la prioridad en el orden de negocio, para ordenar en la base de datos.
  ///
  /// Ordenar por la columna de texto daría High, Low, Normal, Urgent —alfabético, que no
  /// significa nada—. Esto se traduce a un CASE y se construye a partir de
  /// <see cref="TaskPriority.All()"/>, así que el orden vive en el dominio y en un solo
  /// sitio: añadir o reordenar prioridades no obliga a tocar esta consulta.
  /// </summary>
  private static readonly Expression<Func<WorkTask, int>> RangoDePrioridad = ConstruirRangoDePrioridad();

  private static Expression<Func<WorkTask, int>> ConstruirRangoDePrioridad()
  {
    var tarea = Expression.Parameter(typeof(WorkTask), "t");
    var valor = Expression.Property(
        Expression.Property(tarea, nameof(WorkTask.Priority)),
        nameof(TaskPriority.Value));

    var todas = TaskPriority.All();

    // Una prioridad que no esté en la lista —una fila vieja con la columna vacía— se va al
    // final en lugar de colarse en la cabecera como haría el 0.
    Expression cuerpo = Expression.Constant(todas.Count);

    for (var i = todas.Count - 1; i >= 0; i--)
    {
      cuerpo = Expression.Condition(
          Expression.Equal(valor, Expression.Constant(todas[i].Value)),
          Expression.Constant(i),
          cuerpo);
    }

    return Expression.Lambda<Func<WorkTask, int>>(cuerpo, tarea);
  }

  public async Task<PagedResult<TaskDto>> GetByTenantAsync(
      Guid tenantId, Guid? projectId, Guid? assigneeId, string? status,
      int page, int pageSize, CancellationToken ct = default)
  {
      return await GetByTenantWithPaginationAsync(tenantId, projectId, assigneeId, status, null, null, null, new PaginationRequest { Page = page, PageSize = pageSize }, ct);
  }

  public async Task<PagedResult<TaskDto>> GetByTenantWithPaginationAsync(Guid tenantId, Guid? projectId, Guid? assigneeId, string? status, string? priority, string? filter, Guid? userId, PaginationRequest pagination, CancellationToken ct = default)
  {
    var query = context.Tasks.AsNoTracking().Where(t => t.TenantId == tenantId);

    if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
    if (assigneeId.HasValue) query = query.Where(t => t.AssigneeId == assigneeId.Value);
    if (!string.IsNullOrEmpty(status)) query = query.Where(t => t.Status.Value == status || t.Status.Name == status);
    if (!string.IsNullOrEmpty(priority)) query = query.Where(t => t.Priority.Value == priority || t.Priority.Name == priority);

    if (!string.IsNullOrWhiteSpace(filter) && userId.HasValue)
    {
        if (filter.Equals("mine", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.AssigneeId == userId.Value);
        }
        else if (filter.Equals("team", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => EF.Functions.JsonContains(t.TagIds, userId.Value.ToString()));
        }
    }

    if (pagination.StartDate.HasValue)
    {
        var sd = DateOnly.FromDateTime(pagination.StartDate.Value);
        query = query.Where(t => t.DueDate >= sd);
    }
    if (pagination.EndDate.HasValue)
    {
        var ed = DateOnly.FromDateTime(pagination.EndDate.Value);
        query = query.Where(t => t.DueDate <= ed);
    }

    var limitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3));
    query = query.Where(t => !((t.Status.Value == "Done" || t.Status.Name == "Done") && t.DueDate < limitDate));

    var totalCount = await query.CountAsync(ct);

    // Apply Sorting
    var desc = pagination.SortDirection?.ToLower() == "desc";
    query = pagination.SortColumn?.ToLower() switch
    {
        "title" => desc ? query.OrderByDescending(t => t.Title.Value) : query.OrderBy(t => t.Title.Value),
        "status" => desc ? query.OrderByDescending(t => t.Status.Value) : query.OrderBy(t => t.Status.Value),
        "priority" => desc ? query.OrderByDescending(RangoDePrioridad) : query.OrderBy(RangoDePrioridad),
        "duedate" => desc ? query.OrderByDescending(t => t.DueDate) : query.OrderBy(t => t.DueDate),
        "estimatedhours" => desc ? query.OrderByDescending(t => t.EstimatedHours) : query.OrderBy(t => t.EstimatedHours),
        _ => query.OrderByDescending(t => t.DueDate)
    };

    var items = await query
        .Skip(pagination.Skip).Take(pagination.Take)
        .Select(t => new TaskDto(t.Id, t.TenantId, t.ProjectId, t.Title.Value, t.Description,
            t.Status.Value, t.Priority.Value, t.AssigneeId, t.CreatedById, t.EstimatedHours, t.DueDate))
        .ToListAsync(ct);

    return PagedResult<TaskDto>.Create(items, totalCount, pagination.Page, pagination.PageSize);
  }

  public async Task<TaskDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
  {
    return await context.Tasks.AsNoTracking()
        .Where(t => t.TenantId == tenantId && t.Id == id)
        .Select(t => new TaskDto(t.Id, t.TenantId, t.ProjectId, t.Title.Value, t.Description,
            t.Status.Value, t.Priority.Value, t.AssigneeId, t.CreatedById, t.EstimatedHours, t.DueDate))
        .FirstOrDefaultAsync(ct);
  }
}