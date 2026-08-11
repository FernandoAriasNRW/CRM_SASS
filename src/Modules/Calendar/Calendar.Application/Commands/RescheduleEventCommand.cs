using BuildingBlocks.Application.Abstractions;
using Calendar.Application.DTOs;

namespace Calendar.Application.Commands;


public sealed record RescheduleEventCommand(
    Guid TenantId,
    Guid EventId,
    Guid ActorId,
    DateTime NewStartTime,
    DateTime NewEndTime
) : ICommand<CalendarEventDto>, IWebhookTriggered
{
    public string WebhookEventName => "calendar.event.rescheduled";
}