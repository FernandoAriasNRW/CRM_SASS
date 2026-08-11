using BuildingBlocks.Domain;
using Calendar.Domain.Entities;

namespace Calendar.Application.Abstractions.Repositories;

/// <summary>
/// Repository para operaciones de ESCRITURA del módulo Calendar. Separación CQRS: Este repository solo se usa para
/// Commands. Para Queries usar ICalendarEventQueries.
/// </summary>
public interface ICalendarEventRepository
{
  /// <summary>
  /// Agrega un nuevo evento de calendario.
  /// </summary>
  Task AddAsync(CalendarEvent calendarEvent, CancellationToken ct = default);

  /// <summary>
  /// Actualiza un evento de calendario existente.
  /// </summary>
  Task UpdateAsync(CalendarEvent calendarEvent, CancellationToken ct = default);

  /// <summary>
  /// Obtiene un evento por su ID (para uso en Commands). Para Queries usar ICalendarEventQueries.
  /// </summary>
  Task<CalendarEvent?> GetByIdAsync(
      Guid tenantId,
      Guid id,
      CancellationToken ct = default,
      bool includeDeleted = false);

  Task DeleteAsync(Guid tenantId, Guid eventId, CancellationToken ct);
}