using BuildingBlocks.Application.Abstractions;

namespace Ticketing.Application.Commands;

public sealed record AssignTicketCommand(
    Guid TenantId,
    Guid TicketId,
    Guid AgentId
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "ticket.assigned";
}