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

    public static WebhookSubscription Create(DateTime nowUtc, Guid tenantId, string eventName, string targetUrl, string secret)
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
            CreatedAt = nowUtc
        };
    }

    public void Update(DateTime nowUtc, string? targetUrl, string? secret)
    {
        if (!string.IsNullOrWhiteSpace(targetUrl)) TargetUrl = targetUrl;
        if (!string.IsNullOrWhiteSpace(secret)) Secret = secret;
        UpdatedAt = nowUtc;
    }

    public void Deactivate(DateTime nowUtc)
    {
        IsActive = false;
        UpdatedAt = nowUtc;
    }

    public void Activate(DateTime nowUtc)
    {
        IsActive = true;
        UpdatedAt = nowUtc;
    }
}
