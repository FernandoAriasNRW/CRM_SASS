using BuildingBlocks.Application.Abstractions;

namespace Ticketing.Application.Commands;

public sealed record UpdateTicketCommand(
    Guid TenantId,
    Guid TicketId,
    string? Title,
    string? Description,
    string? Priority,
    string? Status,
    Guid? AssignedAgentId
) : ICommand<bool>, IWebhookTriggered
{
    public string WebhookEventName => "ticket.updated";
}