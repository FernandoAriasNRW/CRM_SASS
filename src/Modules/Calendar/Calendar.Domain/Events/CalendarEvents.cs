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
/// Evento de dominio publicado cuando se cancela (soft delete) un evento.
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