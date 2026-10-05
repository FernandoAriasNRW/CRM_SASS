using BuildingBlocks.Domain.Primitives;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Infrastructure.DomainEvents;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default);
}

/// <summary>
/// Reparte los eventos de dominio en proceso, a los <c>INotificationHandler</c> de MediatR.
///
/// <b>No escribe en el outbox.</b> Eso lo hace <c>UnitOfWork</c> al guardar, antes de llamar
/// aquí. Antes lo hacían los dos, así que cada evento guardado con
/// <c>SaveChangesAndDispatchAsync</c> quedaba dos veces en <c>outbox_messages</c>, con la misma
/// carga y el mismo <c>EventId</c>, y el <c>OutboxDispatcherWorker</c> lo publicaba dos veces.
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var @event in events)
        {
            using var scope = serviceProvider.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            
            var notificationType = typeof(BuildingBlocks.Application.Events.DomainEventNotification<>).MakeGenericType(@event.GetType());
            var notification = Activator.CreateInstance(notificationType, @event) as INotification;
            
            if (notification is not null)
            {
                await publisher.Publish(notification, ct);
            }
        }
    }
}
