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

    // Con nombre. La firma es `Create(items, totalCount, page, pageSize)` y aquí se pasaba
    // `(dtos, page, pageSize, totalCount)`: tres enteros seguidos compilan en cualquier orden, así
    // que el calendario respondía «230 páginas de 1 elemento» sin que nada fallara. Es el mismo
    // error que el de la disposición del panel, donde tres `Guid` seguidos se cruzaron igual.
    return PagedResult<CalendarEventDto>.Create(
        items: dtos, totalCount: totalCount, page: pagination.Page, pageSize: pagination.PageSize);
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

    // Con nombre. La firma es `Create(items, totalCount, page, pageSize)` y aquí se pasaba
    // `(dtos, page, pageSize, totalCount)`: tres enteros seguidos compilan en cualquier orden, así
    // que el calendario respondía «230 páginas de 1 elemento» sin que nada fallara. Es el mismo
    // error que el de la disposición del panel, donde tres `Guid` seguidos se cruzaron igual.
    return PagedResult<CalendarEventDto>.Create(
        items: dtos, totalCount: totalCount, page: pagination.Page, pageSize: pagination.PageSize);
  }

  /// <summary>
  /// Pasa la entidad a DTO reutilizando el mapeo de la capa de aplicación.
  ///
  /// <b>Estaba escrito dos veces</b>, aquí y en <c>CalendarEventDtoExtensions.ToDto</c>, y las dos
  /// copias ya habían empezado a separarse: una traducía el tipo con <c>FromValue(...)?.Name ??
  /// "Unknown"</c> y la otra con la propiedad de la entidad. Cuando se marcaron las fechas como
  /// UTC —lo que evita que el navegador corra los eventos el desfase horario— sólo se arregló una
  /// de las dos, así que la lista seguía mal mientras el detalle salía bien.
  /// </summary>
  private static CalendarEventDto MapToDto(CalendarEvent entity) => entity.ToDto();
}