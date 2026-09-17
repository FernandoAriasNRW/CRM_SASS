using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;

namespace Ticketing.Application.Commands;

public sealed record CloseTicketCommand(
    Guid TenantId,
    Guid TicketId
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string EntityType => "Ticket";
    public Guid EntityId => TicketId;
    public string RequiredPermission => "Write";

    public string WebhookEventName => "ticket.closed";
}
