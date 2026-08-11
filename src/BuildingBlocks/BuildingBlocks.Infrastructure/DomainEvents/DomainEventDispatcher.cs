using BuildingBlocks.Domain.Primitives;
using BuildingBlocks.Infrastructure.Outbox;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace BuildingBlocks.Infrastructure.DomainEvents;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default);
}

public sealed class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    IOutboxService outboxService) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var @event in events)
        {
            // 1. Dispatch in-process via MediatR INotificationHandler<T>
            using var scope = serviceProvider.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            
            var notificationType = typeof(BuildingBlocks.Application.Events.DomainEventNotification<>).MakeGenericType(@event.GetType());
            var notification = Activator.CreateInstance(notificationType, @event) as INotification;
            
            if (notification is not null)
            {
                await publisher.Publish(notification, ct);
            }

            // 2. Persist to Outbox for cross-module integration (eventual consistency)
            await outboxService.AddMessageAsync(
                @event.GetType().FullName ?? @event.GetType().Name,
                JsonSerializer.Serialize(@event, @event.GetType()),
                ct);
        }
    }
}
