using BuildingBlocks.Domain.Primitives;

namespace Projects.Domain.Events;

public sealed record ProjectCreatedEvent(Guid ProjectId, Guid TenantId, string Name) : DomainEvent;

public sealed record ProjectUpdatedEvent(Guid ProjectId, Guid TenantId, string Name, string Status) : DomainEvent;

public sealed record ProjectDeletedEvent(Guid ProjectId, Guid TenantId, Guid DeletedBy) : DomainEvent;

public sealed record ProjectStatusChangedEvent(Guid ProjectId, Guid TenantId, string NewStatus) : DomainEvent;