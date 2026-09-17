using BuildingBlocks.Application.Authorization;
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
) : ICommand<bool>, IWebhookTriggered, IAuthorizeEntity
{
    public string EntityType => "Ticket";
    public Guid EntityId => TicketId;
    public string RequiredPermission => "Write";

    public string WebhookEventName => "ticket.updated";
}