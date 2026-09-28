using BuildingBlocks.Application.Abstractions;
using Calendar.Application.DTOs;

namespace Calendar.Application.Commands;

/// <summary>
/// Anula el evento dejándolo a la vista, tachado.
///
/// <b>No es <see cref="MoveEventToTrashCommand"/>.</b> Ese manda a la papelera, pese al nombre: es el
/// que había, se llamaba «cancel» y hacía desaparecer la reunión del calendario. Quien mira el
/// jueves necesita ver que se anuló, no encontrarse un hueco.
/// </summary>
public sealed record CancelEventCommand(
    Guid TenantId,
    Guid EventId,
    Guid UserId,
    string? Reason = null
) : ICommand<CalendarEventDto>, IWebhookTriggered
{
    public string WebhookEventName => "calendar.event.cancelled";
}
