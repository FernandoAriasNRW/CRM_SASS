using BuildingBlocks.Application.Abstractions;
using Calendar.Application.DTOs;

namespace Calendar.Application.Commands;

/// <summary>
/// Enlaza el evento con un proyecto, una tarea o un ticket.
///
/// Los tres campos se mandan siempre, también en nulo: es la única forma de que «quitar el
/// enlace» se distinga de «no tocarlo». Con campos opcionales, desenlazar sería imposible.
/// </summary>
public sealed record LinkEventCommand(
    Guid TenantId,
    Guid EventId,
    Guid? ProjectId,
    Guid? TaskId,
    Guid? TicketId
) : ICommand<CalendarEventDto>, IWebhookTriggered
{
    public string WebhookEventName => "calendar.event.linked";
}
