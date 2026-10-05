using BuildingBlocks.Application.Events;
using Ticketing.Domain.Events;
using Ticketing.Domain.ValueObjects;
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
    
    // El estado va por su nombre, como en TicketDto. Iba el valor numérico (1 a 5) y el tablero lo
    // traducía con una lista que empezaba en 0 y no tenía PendingInfo: cada ticket movido por
    // otra persona aparecía en la columna siguiente, o en ninguna.
    var status = TicketStatus.FromValue<TicketStatus>(domainEvent.NewStatus).Name;

    // Broadcast to the tenant's tickets group
    await hubContext.Clients.Group(domainEvent.TenantId.ToString())
        .SendAsync("ticket_moved", new { ticketId = domainEvent.TicketId, status }, cancellationToken);
        
    logger.LogInformation("Broadcasted ticket {TicketId} moved to status {Status} in tenant {TenantId}", domainEvent.TicketId, domainEvent.NewStatus, domainEvent.TenantId);
  }
}
