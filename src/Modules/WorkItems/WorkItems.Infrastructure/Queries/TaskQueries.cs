using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using WorkItems.Application.Abstractions.Queries;
using WorkItems.Application.DTOs;
using WorkItems.Infrastructure.Persistence;

namespace WorkItems.Infrastructure.Queries;

public sealed class TaskQueries(WorkItemsDbContext context) : ITaskQueries
{
  public async Task<PagedResult<TaskDto>> GetByTenantAsync(
      Guid tenantId, Guid? projectId, Guid? assigneeId, string? status,
      int page, int pageSize, CancellationToken ct = default)
  {
      return await GetByTenantWithPaginationAsync(tenantId, projectId, assigneeId, status, null, null, new PaginationRequest { Page = page, PageSize = pageSize }, ct);
  }

  public async Task<PagedResult<TaskDto>> GetByTenantWithPaginationAsync(Guid tenantId, Guid? projectId, Guid? assigneeId, string? status, string? filter, Guid? userId, PaginationRequest pagination, CancellationToken ct = default)
  {
    var query = context.Tasks.AsNoTracking().Where(t => t.TenantId == tenantId);

    if (projectId.HasValue) query = query.Where(t => t.ProjectId == projectId.Value);
    if (assigneeId.HasValue) query = query.Where(t => t.AssigneeId == assigneeId.Value);
    if (!string.IsNullOrEmpty(status)) query = query.Where(t => t.Status.Value == status || t.Status.Name == status);

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
        "duedate" => desc ? query.OrderByDescending(t => t.DueDate) : query.OrderBy(t => t.DueDate),
        "estimatedhours" => desc ? query.OrderByDescending(t => t.EstimatedHours) : query.OrderBy(t => t.EstimatedHours),
        _ => query.OrderByDescending(t => t.DueDate)
    };

    var items = await query
        .Skip(pagination.Skip).Take(pagination.Take)
        .Select(t => new TaskDto(t.Id, t.TenantId, t.ProjectId, t.Title.Value, t.Description,
            t.Status.Value, t.AssigneeId, t.CreatedById, t.EstimatedHours, t.DueDate))
        .ToListAsync(ct);

    return PagedResult<TaskDto>.Create(items, totalCount, pagination.Page, pagination.PageSize);
  }

  public async Task<TaskDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
  {
    return await context.Tasks.AsNoTracking()
        .Where(t => t.TenantId == tenantId && t.Id == id)
        .Select(t => new TaskDto(t.Id, t.TenantId, t.ProjectId, t.Title.Value, t.Description,
            t.Status.Value, t.AssigneeId, t.CreatedById, t.EstimatedHours, t.DueDate))
        .FirstOrDefaultAsync(ct);
  }
}