
namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;

public class WebhookDelivery
{
    public Guid Id { get; private set; }
    public Guid WebhookId { get; private set; }
    public string Payload { get; private set; } = default!;
    public int Attempts { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public string Status { get; private set; } = "Pending";

    private WebhookDelivery() { }

    public WebhookDelivery(Guid webhookId, string payload)
    {
        Id = Guid.NewGuid();
        WebhookId = webhookId;
        Payload = payload;
        Attempts = 0;
        Status = "Pending";
    }

    public void MarkSuccess()
    {
        Status = "Delivered";
        DeliveredAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Attempts++;
        Status = "Failed";
    }
}