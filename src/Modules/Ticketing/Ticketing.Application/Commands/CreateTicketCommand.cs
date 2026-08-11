using BuildingBlocks.Application.Abstractions;
using Ticketing.Domain.Entities;

namespace Ticketing.Application.Commands;


public sealed record CreateTicketCommand(
    Guid TenantId,
    Guid CustomerId,
    string Title,
    string Description,
    string Priority
) : ICommand<Ticket>, IWebhookTriggered
{
    public string WebhookEventName => "ticket.created";
}
