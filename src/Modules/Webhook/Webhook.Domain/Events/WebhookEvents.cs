using BuildingBlocks.Domain.Primitives;

namespace Webhook.Domain.Events;

public sealed record WebhookSubscriptionCreatedEvent(Guid SubscriptionId, Guid TenantId, string EventName) : DomainEvent;
public sealed record WebhookSubscriptionDeletedEvent(Guid SubscriptionId, Guid TenantId) : DomainEvent;
public sealed record WebhookSubscriptionUpdatedEvent(Guid SubscriptionId, Guid TenantId) : DomainEvent;
