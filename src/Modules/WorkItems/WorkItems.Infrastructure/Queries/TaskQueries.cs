using System.Linq.Expressions;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using WorkItems.Application.Abstractions.Queries;
using WorkItems.Application.DTOs;
using WorkItems.Domain.Entities;
using WorkItems.Domain.ValueObjects;
using WorkItems.Infrastructure.Persistence;
// Alias: `TaskStatus` choca con System.Threading.Tasks.TaskStatus.
using EstadoDeTarea = WorkItems.Domain.ValueObjects.TaskStatus;

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

  /// <summary>El estado que cuenta como subtarea terminada, tomado del dominio.</summary>
  private static readonly EstadoDeTarea EstadoCompletado = EstadoDeTarea.Done;

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
      return await GetByTenantWithPaginationAsync(tenantId, projectId, assigneeId, status, null, null, null, null, false, new PaginationRequest { Page = page, PageSize = pageSize }, ct);
  }

  public async Task<PagedResult<TaskDto>> GetByTenantWithPaginationAsync(Guid tenantId, Guid? projectId, Guid? assigneeId, string? status, string? priority, string? filter, Guid? userId, Guid? parentTaskId, bool incluirSubtareas, PaginationRequest pagination, CancellationToken ct = default)
  {
    var query = context.Tasks.AsNoTracking().Where(t => t.TenantId == tenantId);

    // Las subtareas de una tarea concreta, o —por defecto— sólo las de primer nivel: un
    // tablero con las subtareas mezcladas entre las tareas es ruido, y la cuenta de la
    // paginación dejaría de significar «tareas».
    if (parentTaskId.HasValue)
      query = query.Where(t => t.ParentTaskId == parentTaskId.Value);
    else if (!incluirSubtareas)
      query = query.Where(t => t.ParentTaskId == null);

    if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
    // Se mira el conjunto de responsables y además el campo del principal. Con los datos al día
    // el segundo es redundante, pero una fila que se hubiera quedado sin traspasar desaparecería
    // del filtro sin dar ningún error, y eso es justo la clase de silencio que este proyecto ya
    // ha pagado dos veces.
    if (assigneeId.HasValue)
      query = query.Where(t => t.AssigneeId == assigneeId.Value
                               || t.Assignees.Any(a => a.UserId == assigneeId.Value));
    if (!string.IsNullOrEmpty(status)) query = query.Where(t => t.Status.Value == status || t.Status.Name == status);
    if (!string.IsNullOrEmpty(priority)) query = query.Where(t => t.Priority.Value == priority || t.Priority.Name == priority);

    if (!string.IsNullOrWhiteSpace(filter) && userId.HasValue)
    {
        if (filter.Equals("mine", StringComparison.OrdinalIgnoreCase))
        {
            // «Mis tareas» son las que respondo, sea como principal o como uno más.
            query = query.Where(t => t.AssigneeId == userId.Value
                                     || t.Assignees.Any(a => a.UserId == userId.Value));
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

    var items = await Proyectar(query.Skip(pagination.Skip).Take(pagination.Take), tenantId)
        .ToListAsync(ct);

    return PagedResult<TaskDto>.Create(items, totalCount, pagination.Page, pagination.PageSize);
  }

  public async Task<TaskDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
  {
    return await Proyectar(
            context.Tasks.AsNoTracking().Where(t => t.TenantId == tenantId && t.Id == id),
            tenantId)
        .FirstOrDefaultAsync(ct);
  }

  /// <summary>
  /// Proyección al DTO, en un solo sitio para que la lista y el detalle no se separen.
  ///
  /// El progreso de las subtareas va como dos subconsultas correlacionadas: se calcula en la
  /// base y no se guarda en la tarea, porque un contador denormalizado se desincroniza en
  /// cuanto una subtarea se mueve o se borra por otra vía y entonces la interfaz miente sin
  /// que nada falle.
  /// </summary>
  private IQueryable<TaskDto> Proyectar(IQueryable<WorkTask> query, Guid tenantId)
  {
    var completado = EstadoCompletado.Value;

    return query.Select(t => new TaskDto(
        t.Id, t.TenantId, t.ProjectId, t.Title.Value, t.Description,
        t.Status.Value, t.Priority.Value, t.AssigneeId, t.CreatedById,
        t.EstimatedHours, t.DueDate,
        t.ParentTaskId,
        context.Tasks.Count(s => s.TenantId == tenantId && s.ParentTaskId == t.Id),
        context.Tasks.Count(s => s.TenantId == tenantId && s.ParentTaskId == t.Id
                                 && (s.Status.Value == completado || s.Status.Name == completado)),
        context.TaskDependencies.Count(d => d.TenantId == tenantId && d.TaskId == t.Id),
        context.TaskDependencies.Count(d => d.TenantId == tenantId && d.DependsOnTaskId == t.Id),
        t.Assignees.Select(a => a.UserId).ToList(),
        t.Checklist.Count,
        t.Checklist.Count(i => i.Hecho)));
  }

  public async Task<TaskDependenciesDto> GetDependenciesAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
  {
    // Dos consultas y no una: son dos conjuntos distintos, y unirlos obligaría a etiquetar
    // cada fila con su dirección para volver a separarlas en memoria.
    var bloqueadaPor = await ReferenciasAsync(
        context.TaskDependencies.Where(d => d.TenantId == tenantId && d.TaskId == taskId)
            .Select(d => d.DependsOnTaskId), tenantId, ct);

    var bloqueaA = await ReferenciasAsync(
        context.TaskDependencies.Where(d => d.TenantId == tenantId && d.DependsOnTaskId == taskId)
            .Select(d => d.TaskId), tenantId, ct);

    return new TaskDependenciesDto(bloqueadaPor, bloqueaA);
  }

  public async Task<IReadOnlyList<ChecklistItemDto>> GetChecklistAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
      => await context.Tasks.AsNoTracking()
          .Where(t => t.TenantId == tenantId && t.Id == taskId)
          .SelectMany(t => t.Checklist)
          .OrderBy(i => i.Posicion)
          .Select(i => new ChecklistItemDto(i.Id, i.Texto, i.Hecho, i.Posicion))
          .ToListAsync(ct);

  private async Task<IReadOnlyList<TaskDependencyRefDto>> ReferenciasAsync(
      IQueryable<Guid> ids, Guid tenantId, CancellationToken ct)
      => await context.Tasks.AsNoTracking()
          .Where(t => t.TenantId == tenantId && ids.Contains(t.Id))
          .OrderBy(t => t.Title.Value)
          .Select(t => new TaskDependencyRefDto(t.Id, t.Title.Value, t.Status.Value, t.Priority.Value))
          .ToListAsync(ct);
}