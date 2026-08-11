using BuildingBlocks.Application.Behaviors;
using MediatR;
using Webhook.Application.Abstractions;

namespace Webhook.Application.Handlers;

/// <summary>
/// Escucha WebhookEventNotification publicada por WebhookDispatchBehavior
/// y la reenvía a IWebhookDispatchService que hace el HTTP POST a los suscriptores.
/// </summary>
public sealed class WebhookEventNotificationHandler(IWebhookDispatchService dispatchService)
    : INotificationHandler<WebhookEventNotification>
{
    public async Task Handle(WebhookEventNotification notification, CancellationToken ct)
        => await dispatchService.DispatchAsync(
            notification.EventName,
            notification.TenantId,
            notification.EventData,
            ct);
}
