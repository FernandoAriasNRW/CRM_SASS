using BuildingBlocks.Application.Abstractions;

namespace Calendar.Application.Commands;


public sealed record CancelEventCommand(
    Guid TenantId,
    Guid EventId,
    Guid DeletedBy
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "calendar.event.cancelled";
}
