using BuildingBlocks.Application.Events;
using Communication.Application.Abstractions.Repositories;
using Communication.Application.DTOs;
using Communication.Domain.Events;
using Communication.Presentation.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Communication.Presentation.EventHandlers;

public sealed class MessageAddedEventHandler(
    IMessageRepository messageRepository,
    IHubContext<ChatHub> hubContext,
    ILogger<MessageAddedEventHandler> logger) : INotificationHandler<DomainEventNotification<MessageAddedEvent>>
{
  private readonly IMessageRepository _messageRepository = messageRepository;
  private readonly IHubContext<ChatHub> _hubContext = hubContext;
  private readonly ILogger<MessageAddedEventHandler> _logger = logger;

  public async Task Handle(DomainEventNotification<MessageAddedEvent> notification, CancellationToken cancellationToken)
  {
    var domainEvent = notification.DomainEvent;
    
    var message = await _messageRepository.GetByIdAsync(domainEvent.TenantId, domainEvent.MessageId, false, cancellationToken);
    
    if (message is null)
    {
      _logger.LogWarning("Message {MessageId} not found when trying to broadcast via SignalR", domainEvent.MessageId);
      return;
    }

    var dto = MessageDto.FromDomain(message);

    // Enviar al grupo (canal) correspondiente
    await _hubContext.Clients.Group($"channel_{domainEvent.ConversationId}")
        .SendAsync("message_received", dto, cancellationToken);
        
    _logger.LogInformation("Broadcasted message {MessageId} to channel {ChannelId}", domainEvent.MessageId, domainEvent.ConversationId);
  }
}
