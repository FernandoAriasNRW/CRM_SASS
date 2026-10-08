using BuildingBlocks.Application.Behaviors;
using MediatR;
using Webhook.Application.Abstractions;
using Webhook.Application.Abstractions.Repositories;
using Webhook.Domain.Entities;

namespace Webhook.Application.Handlers;

/// <summary>
/// Convierte un evento en envíos: uno por cada suscripción activa de la organización que lo quiere.
///
/// <b>No llama a nadie.</b> Deja los envíos guardados y avisa al trabajo de entrega. Antes el POST
/// se hacía aquí, dentro de la petición que causó el evento: crear una tarea esperaba a que
/// contestara un servidor ajeno, y si fallaba no quedaba ni rastro.
/// </summary>
public sealed class WebhookEventNotificationHandler(
    TimeProvider timeProvider,
    IWebhookSubscriptionRepository subscriptions,
    IWebhookDeliveryRepository deliveries,
    IWebhookDeliverySignal signal,
    IWebhookUnitOfWork unitOfWork) : INotificationHandler<WebhookEventNotification>
{
    public async Task Handle(WebhookEventNotification notification, CancellationToken ct)
    {
        var interested = await subscriptions.GetActiveForEventAsync(notification.TenantId, notification.EventName, ct);
        if (interested.Count == 0) return;

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var subscription in interested)
        {
            var delivery = WebhookDelivery.Create(now, notification.TenantId, subscription.Id, notification.EventName,
                id => WebhookPayload.Build(id, notification.EventName, notification.TenantId, now,
                    notification.Input, notification.Result));

            await deliveries.AddAsync(delivery, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        signal.Notify();
    }
}
