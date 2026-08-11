using BuildingBlocks.Domain.Primitives;

namespace WorkItems.Domain.Events;

public sealed record TaskCreatedEvent(Guid TaskId, Guid TenantId, Guid ProjectId, Guid AssigneeId) : DomainEvent;

public sealed record TaskStatusChangedEvent(Guid TaskId, Guid TenantId, Guid ProjectId, string OldStatus, string NewStatus) : DomainEvent;

public sealed record TaskAssignedEvent(Guid TaskId, Guid TenantId, Guid AssigneeId) : DomainEvent;
