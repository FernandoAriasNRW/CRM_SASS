using BuildingBlocks.Application.Events;
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
    
    // Broadcast to the project's board group
    await hubContext.Clients.Group(domainEvent.ProjectId.ToString())
        .SendAsync("task_moved", new { taskId = domainEvent.TaskId, status = domainEvent.NewStatus }, cancellationToken);
        
    logger.LogInformation("Broadcasted task {TaskId} moved to status {Status} in board {ProjectId}", domainEvent.TaskId, domainEvent.NewStatus, domainEvent.ProjectId);
  }
}
