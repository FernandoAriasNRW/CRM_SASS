using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Calendar.Application.Abstractions.Queries;
using Calendar.Application.DTOs;
using Calendar.Domain.Entities;
using Calendar.Domain.ValueObjects;
using Calendar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Calendar.Infrastructure.Queries;

/// <summary>
/// Implementación del Query Service para Calendar. Retorna directamente DTOs sin pasar por entidades de dominio.
/// </summary>
public sealed class CalendarEventQueries(CalendarDbContext context) : ICalendarEventQueries
{
  private readonly CalendarDbContext _context = context;

  public async Task<CalendarEventDto?> GetByIdAsync(Guid tenantId, Guid eventId, CancellationToken ct = default)
  {
    var evt = await _context.CalendarEvents
        .AsNoTracking()
        .IgnoreQueryFilters() // Para permitir buscar incluso si está eliminado
        .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == eventId, ct);

    return evt is null ? null : MapToDto(evt);
  }

  public async Task<PagedResult<CalendarEventDto>> GetByTenantAsync(
      Guid tenantId,
      DateTime? startDate,
      DateTime? endDate,
      string? type,
      PaginationRequest pagination,
      CancellationToken ct = default)
  {
    var query = _context.CalendarEvents
        .AsNoTracking()
        .Where(e => e.TenantId == tenantId);

    // Filtro automático de soft deleted (usando filtro global)
    if (startDate.HasValue)
      query = query.Where(e => e.StartTime >= startDate.Value);

    if (endDate.HasValue)
      query = query.Where(e => e.EndTime <= endDate.Value);

    if (!string.IsNullOrEmpty(type))
    {
      var typeEnum = Enumeration.FromName<CalendarEventType>(type);
      if (typeEnum is not null)
        query = query.Where(e => e.TypeValue == typeEnum.Value);
    }

    var totalCount = await query.CountAsync(ct);

    var items = await query
        .OrderByDescending(e => e.StartTime)
        .Skip(pagination.Skip)
        .Take(pagination.Take)
        .ToListAsync(ct);

    var dtos = items.Select(MapToDto).ToList();

    return PagedResult<CalendarEventDto>.Create(dtos, pagination.Page, pagination.PageSize, totalCount);
  }

  public async Task<PagedResult<CalendarEventDto>> GetDeletedByTenantAsync(
      Guid tenantId,
      PaginationRequest pagination,
      CancellationToken ct = default)
  {
    var query = _context.CalendarEvents
        .AsNoTracking()
        .IgnoreQueryFilters() // Ignorar filtro global para ver eliminados
        .Where(e => e.TenantId == tenantId && e.IsDeleted);

    var totalCount = await query.CountAsync(ct);

    var items = await query
        .OrderByDescending(e => e.DeletedAt)
        .Skip(pagination.Skip)
        .Take(pagination.Take)
        .ToListAsync(ct);

    var dtos = items.Select(MapToDto).ToList();

    return PagedResult<CalendarEventDto>.Create(dtos, pagination.Page, pagination.PageSize, totalCount);
  }

  private static CalendarEventDto MapToDto(CalendarEvent entity)
  {
    return new CalendarEventDto(
        entity.Id,
        entity.TenantId,
        entity.OrganizerId,
        entity.ProjectId,
        entity.TaskId,
        entity.Title,
        entity.Description,
        CalendarEventType.FromValue<CalendarEventType>(entity.TypeValue)?.Name ?? "Unknown",
        entity.StartTime,
        entity.EndTime,
        entity.Location,
        entity.IsAllDay,
        RecurrencePattern.FromValue<RecurrencePattern>(entity.RecurrenceValue)?.Name ?? "None",
        entity.RecurrenceInterval,
        entity.RecurrenceEndDate,
        entity.CreatedAt,
        entity.IsDeleted,
        entity.DeletedAt,
        entity.DeletedBy);
  }
}