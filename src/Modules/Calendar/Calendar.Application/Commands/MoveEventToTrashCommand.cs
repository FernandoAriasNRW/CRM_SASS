using BuildingBlocks.Application.Abstractions;

namespace Calendar.Application.Commands;


public sealed record MoveEventToTrashCommand(
    Guid TenantId,
    Guid EventId,
    Guid DeletedBy
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "calendar.event.trashed";
}
