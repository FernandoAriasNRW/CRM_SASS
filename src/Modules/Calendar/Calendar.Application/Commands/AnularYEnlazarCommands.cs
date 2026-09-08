using BuildingBlocks.Application.Abstractions;
using Calendar.Application.DTOs;

namespace Calendar.Application.Commands;

/// <summary>
/// Anula el evento dejándolo a la vista, tachado.
///
/// <b>No es <see cref="CancelEventCommand"/>.</b> Ese manda a la papelera, pese al nombre: es el
/// que había, se llamaba «cancel» y hacía desaparecer la reunión del calendario. Quien mira el
/// jueves necesita ver que se anuló, no encontrarse un hueco.
/// </summary>
public sealed record AnularEventoCommand(
    Guid TenantId,
    Guid EventId,
    Guid PorQuien,
    string? Motivo = null
) : ICommand<CalendarEventDto>, IWebhookTriggered
{
    public string WebhookEventName => "calendar.event.anulado";
}

/// <summary>Deshace una anulación: la reunión vuelve a estar en pie.</summary>
public sealed record ReactivarEventoCommand(
    Guid TenantId,
    Guid EventId,
    Guid PorQuien
) : ICommand<CalendarEventDto>;

/// <summary>
/// Enlaza el evento con un proyecto, una tarea o un ticket.
///
/// Los tres campos se mandan siempre, también en nulo: es la única forma de que «quitar el
/// enlace» se distinga de «no tocarlo». Con campos opcionales, desenlazar sería imposible.
/// </summary>
public sealed record EnlazarEventoCommand(
    Guid TenantId,
    Guid EventId,
    Guid? ProjectId,
    Guid? TaskId,
    Guid? TicketId
) : ICommand<CalendarEventDto>, IWebhookTriggered
{
    public string WebhookEventName => "calendar.event.enlazado";
}
