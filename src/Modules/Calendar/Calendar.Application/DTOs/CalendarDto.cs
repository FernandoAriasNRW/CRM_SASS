using Calendar.Domain.Entities;

namespace Calendar.Application.DTOs;

/// <summary>
/// DTO para transferenciar datos de CalendarEvent.
/// No contiene lógica de negocio, solo datos.
/// </summary>
public sealed record CalendarEventDto(
    Guid Id,
    Guid TenantId,
    Guid OrganizerId,
    Guid? ProjectId,
    Guid? TaskId,
    string Title,
    string? Description,
    string Type,
    DateTime StartTime,
    DateTime EndTime,
    string? Location,
    bool IsAllDay,
    string Recurrence,
    int? RecurrenceInterval,
    DateTime? RecurrenceEndDate,
    DateTime CreatedAt,
    bool IsDeleted,
    DateTime? DeletedAt,
    Guid? DeletedBy);

/// <summary>
/// Extensiones para mapeo de DTOs.
/// </summary>
public static class CalendarEventDtoExtensions
{
    /// <summary>
    /// Crea un DTO desde una entidad de dominio.
    /// </summary>
    public static CalendarEventDto ToDto(this CalendarEvent entity)
    {
        return new CalendarEventDto(
            entity.Id,
            entity.TenantId,
            entity.OrganizerId,
            entity.ProjectId,
            entity.TaskId,
            entity.Title,
            entity.Description,
            entity.Type.Name,
            entity.StartTime,
            entity.EndTime,
            entity.Location,
            entity.IsAllDay,
            entity.Recurrence.Name,
            entity.RecurrenceInterval,
            entity.RecurrenceEndDate,
            entity.CreatedAt,
            entity.IsDeleted,
            entity.DeletedAt,
            entity.DeletedBy);
    }

    /// <summary>
    /// Crea un DTO desde una entidad de dominio (método estático para compatibilidad).
    /// </summary>
    public static CalendarEventDto FromDomain(CalendarEvent calendarEvent) => calendarEvent.ToDto();
}