using BuildingBlocks.Application.Events;
using BuildingBlocks.Application.Realtime;
using WorkItems.Domain.Events;
using WorkItems.Presentation.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace WorkItems.Presentation.EventHandlers;

public sealed class TaskStatusChangedEventHandler(
    IHubContext<BoardHub> hubContext,
    ILogger<TaskStatusChangedEventHandler> logger) : INotificationHandler<DomainEventNotification<TaskStatusChangedEvent>>
{
  public async Task Handle(DomainEventNotification<TaskStatusChangedEvent> notification, CancellationToken cancellationToken)
  {
    var domainEvent = notification.DomainEvent;
    
    // Al tablero del proyecto, dentro de la organización de la tarea.
    await hubContext.Clients.Group(RealtimeGroups.Board(domainEvent.TenantId, domainEvent.ProjectId))
        .SendAsync("task_moved", new { taskId = domainEvent.TaskId, status = domainEvent.NewStatus }, cancellationToken);
        
    logger.LogInformation("Broadcasted task {TaskId} moved to status {Status} in board {ProjectId}", domainEvent.TaskId, domainEvent.NewStatus, domainEvent.ProjectId);
  }
}
