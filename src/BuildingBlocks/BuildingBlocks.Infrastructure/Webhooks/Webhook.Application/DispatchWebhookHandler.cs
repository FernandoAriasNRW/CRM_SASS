using MediatR;

namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Application;

public class DispatchWebhookHandler : IRequestHandler<DispatchWebhookCommand>
{
    private readonly IWebhookRepository _webhookRepo;
    private readonly IWebhookDeliveryService _deliveryService;

    public DispatchWebhookHandler(
        IWebhookRepository webhookRepo,
        IWebhookDeliveryService deliveryService)
    {
        _webhookRepo = webhookRepo;
        _deliveryService = deliveryService;
    }

    public async Task Handle(DispatchWebhookCommand request, CancellationToken ct)
    {
        var webhooks = await _webhookRepo.GetByEventAsync(request.Event, ct);

        foreach (var webhook in webhooks.Where(w => w.IsActive))
        {
            await _deliveryService.SendAsync(webhook, request.Payload, ct);
        }

        return;
    }
}