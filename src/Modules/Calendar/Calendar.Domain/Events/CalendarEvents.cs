using BuildingBlocks.Domain.Primitives;

namespace Calendar.Domain.Events;

/// <summary>
/// Evento de dominio publicado cuando se crea un nuevo evento de calendario.
/// </summary>
public sealed record CalendarCreatedEvent(
    Guid Id,
    Guid TenantId,
    Guid OrganizerId,
    string Title) : DomainEvent;

/// <summary>
/// Evento de dominio publicado cuando se reprograma un evento.
/// </summary>
public sealed record CalendarRescheduledEvent(
    Guid Id,
    Guid TenantId,
    DateTime NewStartTime,
    DateTime NewEndTime) : DomainEvent;

/// <summary>
/// Evento de dominio publicado cuando un evento va a la papelera.
///
/// Conserva el nombre <c>CalendarCancelled</c> porque hay suscriptores fuera —los webhooks lo
/// publican con ese nombre— y renombrarlo rompería a quien ya escuche. Lo que significa está en
/// <c>CalendarEvent.MoveToTrash</c>: no es la cancelación de la reunión, es quitarla de en
/// medio. La cancelación de verdad es <see cref="EventoCanceladoEvent"/>.
/// </summary>
public sealed record CalendarCancelledEvent(
    Guid Id,
    Guid TenantId,
    Guid? DeletedBy) : DomainEvent;

public sealed record CalendarUpdatedEvent(
    Guid Id,
    Guid TenantId,
    string? Title,
    DateTime? StartTime,
    DateTime? EndTime,
    string? Description,
    string? Location
  ) : DomainEvent;

/// <summary>
/// El evento se ha anulado pero sigue en el calendario, tachado.
///
/// Es distinto de <see cref="CalendarCancelledEvent"/>, que es irse a la papelera. Se separan
/// porque quien escuche esto querrá avisar a los asistentes —«la reunión del jueves se anula»—, y
/// eso no tiene sentido para un evento que alguien creó por error y borró.
/// </summary>
public sealed record EventoCanceladoEvent(
    Guid Id,
    Guid TenantId,
    Guid PorQuien,
    string? Motivo) : DomainEvent;
