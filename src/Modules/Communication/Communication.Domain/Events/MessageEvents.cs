using BuildingBlocks.Domain.Primitives;

namespace Communication.Domain.Events;

// Message Events
public sealed record MessageAddedEvent(Guid MessageId, Guid ConversationId, Guid TenantId) : DomainEvent;
public sealed record MessageEditedEvent(Guid MessageId, Guid TenantId, DateTime EditedAt) : DomainEvent;
public sealed record MessageDeletedEvent(Guid MessageId, Guid TenantId, Guid DeletedBy) : DomainEvent;