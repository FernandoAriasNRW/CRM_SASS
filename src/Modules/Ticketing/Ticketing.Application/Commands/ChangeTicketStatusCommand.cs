using BuildingBlocks.Application.Abstractions;

namespace Ticketing.Application.Commands;

public sealed record ChangeTicketStatusCommand(
    Guid TenantId,
    Guid TicketId,
    string NewStatus
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "ticket.status_changed";
}