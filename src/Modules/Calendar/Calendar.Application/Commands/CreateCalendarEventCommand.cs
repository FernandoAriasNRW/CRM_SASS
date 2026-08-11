using BuildingBlocks.Application.Abstractions;
using Calendar.Application.DTOs;

namespace Calendar.Application.Commands;

public sealed record CreateCalendarEventCommand(
    Guid TenantId,
    Guid OrganizerId,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string Type,
    Guid? ProjectId = null,
    Guid? TaskId = null,
    string? Description = null,
    string? Location = null,
    bool IsAllDay = false,
    string Recurrence = "None",
    int? RecurrenceInterval = null,
    DateTime? RecurrenceEndDate = null
) : ICommand<CalendarEventDto>, IWebhookTriggered
{
    public string WebhookEventName => "calendar.event.created";
}
