using BuildingBlocks.Application.Events;
using Ticketing.Domain.Events;
using Ticketing.Presentation.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Ticketing.Presentation.EventHandlers;

public sealed class TicketStatusChangedEventHandler(
    IHubContext<TicketsHub> hubContext,
    ILogger<TicketStatusChangedEventHandler> logger) : INotificationHandler<DomainEventNotification<TicketStatusChangedEvent>>
{
  public async Task Handle(DomainEventNotification<TicketStatusChangedEvent> notification, CancellationToken cancellationToken)
  {
    var domainEvent = notification.DomainEvent;
    
    // Broadcast to the tenant's tickets group
    await hubContext.Clients.Group(domainEvent.TenantId.ToString())
        .SendAsync("ticket_moved", new { ticketId = domainEvent.TicketId, status = domainEvent.NewStatus }, cancellationToken);
        
    logger.LogInformation("Broadcasted ticket {TicketId} moved to status {Status} in tenant {TenantId}", domainEvent.TicketId, domainEvent.NewStatus, domainEvent.TenantId);
  }
}
