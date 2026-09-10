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
    Guid? TicketId,
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
    Guid? DeletedBy,
    DateTime? CanceladoEnUtc,
    string? MotivoDeCancelacion);

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
            entity.TicketId,
            entity.Title,
            entity.Description,
            entity.Type.Name,
            EnUtc(entity.StartTime),
            EnUtc(entity.EndTime),
            entity.Location,
            entity.IsAllDay,
            entity.Recurrence.Name,
            entity.RecurrenceInterval,
            EnUtcONulo(entity.RecurrenceEndDate),
            EnUtc(entity.CreatedAt),
            entity.IsDeleted,
            EnUtcONulo(entity.DeletedAt),
            entity.DeletedBy,
            EnUtcONulo(entity.CanceladoEnUtc),
            entity.MotivoDeCancelacion);
    }

    /// <summary>
    /// Crea un DTO desde una entidad de dominio (método estático para compatibilidad).
    /// </summary>
    public static CalendarEventDto FromDomain(CalendarEvent calendarEvent) => calendarEvent.ToDto();

    /// <summary>
    /// Marca la fecha como UTC para que salga con la «Z» al serializar.
    ///
    /// <b>Sin esto, todos los eventos se corrían el desfase horario en la pantalla.</b> Las fechas
    /// se guardan en UTC —vienen de <c>DateTime.UtcNow</c>— pero MySQL las devuelve con
    /// <c>Kind = Unspecified</c>, y entonces el serializador escribe
    /// <c>"2026-09-17T16:00:00"</c>, sin marca de zona. El navegador lee una fecha sin zona
    /// <b>como si fuera local</b>: una reunión creada a las 11:00 en UTC−5 se enseñaba a las
    /// 16:00. Medido, no supuesto.
    ///
    /// Se arregla aquí y no en el navegador a propósito: el que sabe que estas fechas son UTC es
    /// el servidor, y taparlo en el cliente dejaría el mismo tropiezo puesto para los webhooks,
    /// las exportaciones y cualquier otro consumidor.
    /// </summary>
    private static DateTime EnUtc(DateTime fecha)
        => fecha.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
            : fecha.ToUniversalTime();

    private static DateTime? EnUtcONulo(DateTime? fecha) => fecha is null ? null : EnUtc(fecha.Value);
}