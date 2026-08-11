using BuildingBlocks.Domain.Primitives;

namespace Notifications.Domain.Events;

public sealed record NotificationCreatedEvent(Guid NotificationId, Guid TenantId, Guid RecipientUserId) : DomainEvent;
public sealed record NotificationSentEvent(Guid NotificationId, Guid TenantId) : DomainEvent;
public sealed record NotificationReadEvent(Guid NotificationId, Guid TenantId, Guid RecipientUserId) : DomainEvent;
public sealed record NotificationFailedEvent(Guid NotificationId, Guid TenantId, string Reason) : DomainEvent;
public sealed record NotificationDeletedEvent(Guid NotificationId, Guid TenantId, Guid DeletedBy) : DomainEvent;
public sealed record NotificationUpdatedEvent(Guid NotificationId, Guid TenantId) : DomainEvent;