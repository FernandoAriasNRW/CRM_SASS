
namespace BuildingBlocks.Infrastructure.Webhooks.Webhook.Domain;

public class WebhookEntity
{
    public Guid Id { get; private set; }
    public string Url { get; private set; } = default!;
    public string Event { get; private set; } = default!;
    public string Secret { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private WebhookEntity() { }

    public WebhookEntity(string url, string @event, string secret)
    {
        Id = Guid.NewGuid();
        Url = url;
        Event = @event;
        Secret = secret;
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
}