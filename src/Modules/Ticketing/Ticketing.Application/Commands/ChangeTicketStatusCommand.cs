using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;

namespace Ticketing.Application.Commands;

public sealed record ChangeTicketStatusCommand(
    Guid TenantId,
    Guid TicketId,
    string NewStatus
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string EntityType => "Ticket";
    public Guid EntityId => TicketId;
    public string RequiredPermission => "Write";

    public string WebhookEventName => "ticket.status_changed";
}