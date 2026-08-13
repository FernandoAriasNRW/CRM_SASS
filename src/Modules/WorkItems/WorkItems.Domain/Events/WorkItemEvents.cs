using BuildingBlocks.Domain.Primitives;

namespace WorkItems.Domain.Events;

public sealed record TaskCreatedEvent(Guid TaskId, Guid TenantId, Guid ProjectId, Guid AssigneeId) : DomainEvent;

public sealed record TaskStatusChangedEvent(Guid TaskId, Guid TenantId, Guid ProjectId, string OldStatus, string NewStatus) : DomainEvent;

public sealed record TaskAssignedEvent(Guid TaskId, Guid TenantId, Guid AssigneeId) : DomainEvent;

public sealed record TaskPriorityChangedEvent(Guid TaskId, Guid TenantId, Guid ProjectId, string OldPriority, string NewPriority) : DomainEvent;

public sealed record TaskParentChangedEvent(Guid TaskId, Guid TenantId, Guid ProjectId, Guid? OldParentTaskId, Guid? NewParentTaskId) : DomainEvent;

public sealed record TaskDependencyAddedEvent(Guid DependencyId, Guid TenantId, Guid TaskId, Guid DependsOnTaskId) : DomainEvent;

public sealed record TaskDependencyRemovedEvent(Guid DependencyId, Guid TenantId, Guid TaskId, Guid DependsOnTaskId) : DomainEvent;
