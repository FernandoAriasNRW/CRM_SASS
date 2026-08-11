using BuildingBlocks.Application.Abstractions;
using Calendar.Application.DTOs;

namespace Calendar.Application.Commands;

public sealed record RestoreEventCommand(
    Guid TenantId,
    Guid EventId,
    Guid RestoredBy
) : ICommand<CalendarEventDto>, IWebhookTriggered
{
    public string WebhookEventName => "calendar.event.restored";
}
