using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using Ticketing.Domain.Entities;

namespace Ticketing.Application.Commands;


public sealed record CreateTicketCommand(
    Guid TenantId,
    Guid CustomerId,
    string Title,
    string Description,
    string Priority
) : ICommand<Ticket>, IWebhookTriggered, IAuthorizeEntity
{
    public string EntityType => "Ticket";
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";

    public string WebhookEventName => "ticket.created";
}
