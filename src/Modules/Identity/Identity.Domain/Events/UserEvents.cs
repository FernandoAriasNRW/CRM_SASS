using BuildingBlocks.Domain.Primitives;

namespace Identity.Domain.Events;

public sealed record UserCreatedEvent(Guid UserId, Guid TenantId, string Email) : DomainEvent;
public sealed record UserUpdatedEvent(Guid UserId, Guid TenantId) : DomainEvent;
public sealed record UserDeletedEvent(Guid UserId, Guid TenantId, Guid DeletedBy) : DomainEvent;
public sealed record PasswordChangedEvent(Guid UserId) : DomainEvent;
public sealed record UserLoggedInEvent(Guid UserId, Guid TenantId) : DomainEvent;