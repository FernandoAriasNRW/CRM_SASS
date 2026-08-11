using BuildingBlocks.Application.Abstractions;

namespace Ticketing.Application.Commands;

public sealed record CloseTicketCommand(
    Guid TenantId,
    Guid TicketId
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "ticket.closed";
}
