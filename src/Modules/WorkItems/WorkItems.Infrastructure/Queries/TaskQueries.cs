using System.Linq.Expressions;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using WorkItems.Application.Abstractions.Queries;
using WorkItems.Application.DTOs;
using WorkItems.Domain.Entities;
using WorkItems.Domain.ValueObjects;
using WorkItems.Infrastructure.Persistence;
// Alias: `TaskStatus` choca con System.Threading.Tasks.TaskStatus.
using DomainTaskStatus = WorkItems.Domain.ValueObjects.TaskStatus;

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
  private static readonly Expression<Func<WorkTask, int>> PriorityRange = BuildPriorityRange();

  /// <summary>El estado que cuenta como subtarea terminada, tomado del dominio.</summary>
  private static readonly DomainTaskStatus CompletedStatus = DomainTaskStatus.Done;

  private static Expression<Func<WorkTask, int>> BuildPriorityRange()
  {
    var task = Expression.Parameter(typeof(WorkTask), "t");
    var value = Expression.Property(
        Expression.Property(task, nameof(WorkTask.Priority)),
        nameof(TaskPriority.Value));

    var all = TaskPriority.All();

    // Una prioridad que no esté en la lista —una fila vieja con la columna vacía— se va al
    // final en lugar de colarse en la cabecera como haría el 0.
    Expression body = Expression.Constant(all.Count);

    for (var i = all.Count - 1; i >= 0; i--)
    {
      body = Expression.Condition(
          Expression.Equal(value, Expression.Constant(all[i].Value)),
          Expression.Constant(i),
          body);
    }

    return Expression.Lambda<Func<WorkTask, int>>(body, task);
  }

  public async Task<PagedResult<TaskDto>> GetByTenantAsync(
      Guid tenantId, Guid? projectId, Guid? assigneeId, string? status,
      int page, int pageSize, CancellationToken ct = default)
  {
      return await GetByTenantWithPaginationAsync(tenantId, projectId, assigneeId, status, null, null, null, false, new PaginationRequest { Page = page, PageSize = pageSize }, ct);
  }

  public async Task<PagedResult<TaskDto>> GetByTenantWithPaginationAsync(Guid tenantId, Guid? projectId, Guid? assigneeId, string? status, string? priority, ViewScope? viewScope, Guid? parentTaskId, bool includeSubtasks, PaginationRequest pagination, CancellationToken ct = default)
  {
    var scope = viewScope ?? ViewScope.None;

    // La papelera y el archivo están fuera de lo que el filtro global deja ver, así que no basta
    // con un `Where`: hay que abrir el alcance antes de construir la consulta. El ámbito se
    // cierra al terminar el método.
    using var _ = context.IncludeHidden(
        deleted: scope.Is(ViewFilters.Trash),
        archived: scope.Is(ViewFilters.Archived));

    var query = context.Tasks.AsNoTracking().Where(t => t.TenantId == tenantId);

    // Las subtareas de una tarea concreta, o —por defecto— sólo las de primer nivel: un
    // tablero con las subtareas mezcladas entre las tareas es ruido, y la cuenta de la
    // paginación dejaría de significar «tareas».
    if (parentTaskId.HasValue)
      query = query.Where(t => t.ParentTaskId == parentTaskId.Value);
    else if (!includeSubtasks)
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

    var me = scope.UserId;

    if (scope.Is(ViewFilters.Mine) && me.HasValue)
    {
        // «Mis tareas» son las que respondo, sea como principal o como uno más.
        query = query.Where(t => t.AssigneeId == me.Value
                                 || t.Assignees.Any(a => a.UserId == me.Value));
    }
    else if (scope.Is(ViewFilters.MyTeam) && me.HasValue)
    {
        query = query.Where(t => EF.Functions.JsonContains(t.TagIds, me.Value.ToString()));
    }
    else if (scope.Is(ViewFilters.CreatedByMe) && me.HasValue)
    {
        // «Creado por mí» es distinto de «mío»: una tarea que abrí y pasó a otra persona
        // sigue siendo mía en el sentido de que la escribí yo, y es como se busca.
        query = query.Where(t => t.CreatedById == me.Value);
    }
    else if (scope.Is(ViewFilters.Favorites))
    {
        // Sin marcados, cero resultados y no «todos». `EF.Constant` incrusta los identificadores
        // porque el proveedor de MySQL no traduce una colección parametrizada; es seguro porque
        // la lista está acotada a 200 por persona y tipo.
        var favorites = scope.Favorites.ToArray();
        query = query.Where(t => EF.Constant(favorites).Contains(t.Id));
    }
    else if (scope.Is(ViewFilters.SharedWithMe))
    {
        var sharedWithMe = scope.SharedWithMe.ToArray();
        query = query.Where(t => EF.Constant(sharedWithMe).Contains(t.Id));
    }
    else if (scope.Is(ViewFilters.Private) && me.HasValue)
    {
        // Privado es «la llevo yo y no se la he dado a nadie». Se resta lo compartido en lugar
        // de guardar un campo `EsPrivado`, que sería una segunda fuente de verdad.
        var sharedWithOthers = scope.SharedWithOthers.ToArray();
        query = query.Where(t => t.AssigneeId == me.Value && !EF.Constant(sharedWithOthers).Contains(t.Id));
    }
    else if (scope.Is(ViewFilters.Archived))
    {
        query = query.Where(t => t.ArchivedAtUtc != null);
    }
    else if (scope.Is(ViewFilters.Trash))
    {
        query = query.Where(t => t.IsDeleted);
    }

    // Búsqueda por texto, sobre **todas** las tareas del inquilino. Ver la nota equivalente en
    // TicketQueries: en el servidor y no filtrando en el cliente lo que quepa en una página.
    if (pagination.SearchText is { } text)
        query = query.Where(t => t.Title.Value.Contains(text) || t.Description.Contains(text));

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
        "priority" => desc ? query.OrderByDescending(PriorityRange) : query.OrderBy(PriorityRange),
        "duedate" => desc ? query.OrderByDescending(t => t.DueDate) : query.OrderBy(t => t.DueDate),
        "estimatedhours" => desc ? query.OrderByDescending(t => t.EstimatedHours) : query.OrderBy(t => t.EstimatedHours),
        _ => query.OrderByDescending(t => t.DueDate)
    };

    var items = await Project(query.Skip(pagination.Skip).Take(pagination.Take), tenantId)
        .ToListAsync(ct);

    return PagedResult<TaskDto>.Create(items, totalCount, pagination.Page, pagination.PageSize);
  }

  public async Task<TaskDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
  {
    return await Project(
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
  private IQueryable<TaskDto> Project(IQueryable<WorkTask> query, Guid tenantId)
  {
    var completed = CompletedStatus.Value;

    return query.Select(t => new TaskDto(
        t.Id, t.TenantId, t.ProjectId, t.Title.Value, t.Description,
        t.Status.Value, t.Priority.Value, t.AssigneeId, t.CreatedById,
        t.EstimatedHours, t.DueDate,
        t.ParentTaskId,
        context.Tasks.Count(s => s.TenantId == tenantId && s.ParentTaskId == t.Id),
        context.Tasks.Count(s => s.TenantId == tenantId && s.ParentTaskId == t.Id
                                 && (s.Status.Value == completed || s.Status.Name == completed)),
        context.TaskDependencies.Count(d => d.TenantId == tenantId && d.TaskId == t.Id),
        context.TaskDependencies.Count(d => d.TenantId == tenantId && d.DependsOnTaskId == t.Id),
        t.Assignees.Select(a => a.UserId).ToList(),
        t.Checklist.Count,
        t.Checklist.Count(i => i.IsDone),
        t.Recurrence == null
            ? null
            : new RecurrenceDto(t.Recurrence.Frequency, t.Recurrence.Interval,
                                t.Recurrence.NextOccurrence, t.Recurrence.EndDate),
        t.StartDate));
  }

  public async Task<TaskDependenciesDto> GetDependenciesAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
  {
    // Dos consultas y no una: son dos conjuntos distintos, y unirlos obligaría a etiquetar
    // cada fila con su dirección para volver a separarlas en memoria.
    var blockedBy = await ReferencesAsync(
        context.TaskDependencies.Where(d => d.TenantId == tenantId && d.TaskId == taskId)
            .Select(d => d.DependsOnTaskId), tenantId, ct);

    var blocks = await ReferencesAsync(
        context.TaskDependencies.Where(d => d.TenantId == tenantId && d.DependsOnTaskId == taskId)
            .Select(d => d.TaskId), tenantId, ct);

    return new TaskDependenciesDto(blockedBy, blocks);
  }

  public async Task<IReadOnlyList<TaskDependencyEdgeDto>> GetDependencyGraphAsync(Guid tenantId, CancellationToken ct = default)
      => await context.TaskDependencies.AsNoTracking()
          .Where(d => d.TenantId == tenantId)
          .Select(d => new TaskDependencyEdgeDto(d.TaskId, d.DependsOnTaskId))
          .ToListAsync(ct);

  public async Task<IReadOnlyList<ChecklistItemDto>> GetChecklistAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
      => await context.Tasks.AsNoTracking()
          .Where(t => t.TenantId == tenantId && t.Id == taskId)
          .SelectMany(t => t.Checklist)
          .OrderBy(i => i.Position)
          .Select(i => new ChecklistItemDto(i.Id, i.Text, i.IsDone, i.Position))
          .ToListAsync(ct);

  private async Task<IReadOnlyList<TaskDependencyRefDto>> ReferencesAsync(
      IQueryable<Guid> ids, Guid tenantId, CancellationToken ct)
      => await context.Tasks.AsNoTracking()
          .Where(t => t.TenantId == tenantId && ids.Contains(t.Id))
          .OrderBy(t => t.Title.Value)
          .Select(t => new TaskDependencyRefDto(t.Id, t.Title.Value, t.Status.Value, t.Priority.Value))
          .ToListAsync(ct);
}