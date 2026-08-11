using BuildingBlocks.Domain.Primitives;

namespace Webhook.Domain.Entities;

public sealed class WebhookSubscription : AggregateRoot, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string TargetUrl { get; private set; } = string.Empty;
    public string Secret { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private WebhookSubscription() { }

    public static WebhookSubscription Create(Guid tenantId, string eventName, string targetUrl, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUrl);

        return new WebhookSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventName = eventName,
            TargetUrl = targetUrl,
            Secret = secret,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string? targetUrl, string? secret)
    {
        if (!string.IsNullOrWhiteSpace(targetUrl)) TargetUrl = targetUrl;
        if (!string.IsNullOrWhiteSpace(secret)) Secret = secret;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
