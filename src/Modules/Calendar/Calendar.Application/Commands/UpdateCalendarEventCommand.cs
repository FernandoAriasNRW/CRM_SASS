using BuildingBlocks.Application.Abstractions;
using Calendar.Application.DTOs;

namespace Calendar.Application.Commands;

public sealed record UpdateCalendarEventCommand(
    Guid TenantId,
    Guid EventId,
    Guid ActorId,
    string? Title = null,
    DateTime? StartTime = null,
    DateTime? EndTime = null,
    string? Description = null,
    string? Location = null
) : ICommand<CalendarEventDto>, IWebhookTriggered
{
    public string WebhookEventName => "calendar.event.updated";
}