using BuildingBlocks.Domain.Primitives;

namespace Communication.Domain.Events;

// Conversation Events
public sealed record ConversationCreatedEvent(Guid Id, Guid TenantId, string Name) : DomainEvent;
public sealed record ConversationDeletedEvent(Guid Id, Guid TenantId, Guid DeletedBy) : DomainEvent;