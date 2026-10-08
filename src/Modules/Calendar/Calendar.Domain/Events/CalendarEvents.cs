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
/// Se llamaba <c>CalendarCancelledEvent</c> aunque no es la cancelación de la reunión, sino quitarla
/// de en medio (<c>CalendarEvent.MoveToTrash</c>). La cancelación de verdad es
/// <see cref="CalendarEventCancelledEvent"/>. Lo que ven los suscriptores de webhooks no es este
/// nombre sino el del comando: «calendar.event.trashed» (ver <c>WebhookEventCatalog</c>).
/// </summary>
public sealed record CalendarEventTrashedEvent(
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
/// Es distinto de <see cref="CalendarEventTrashedEvent"/>, que es irse a la papelera. Se separan
/// porque quien escuche esto querrá avisar a los asistentes —«la reunión del jueves se anula»—, y
/// eso no tiene sentido para un evento que alguien creó por error y borró.
/// </summary>
public sealed record CalendarEventCancelledEvent(
    Guid Id,
    Guid TenantId,
    Guid CancelledBy,
    string? Reason) : DomainEvent;
