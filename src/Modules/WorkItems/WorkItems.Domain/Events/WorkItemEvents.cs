using BuildingBlocks.Domain.Primitives;

namespace WorkItems.Domain.Events;

// Los tres eventos que reaccionan a un cambio de la tarea llevan, además de lo que cambió, cómo
// queda la tarea: título, estado, prioridad y responsable. Quien los escucha —las automatizaciones,
// por ejemplo— no tiene otra forma de saberlo sin volver a leer la tarea, y sin esos datos una
// condición como «el título contiene…» no se cumpliría nunca.

public sealed record TaskCreatedEvent(
    Guid TaskId, Guid TenantId, Guid ProjectId, Guid AssigneeId,
    string Title, string Status, string Priority) : DomainEvent;

public sealed record TaskStatusChangedEvent(
    Guid TaskId, Guid TenantId, Guid ProjectId, string OldStatus, string NewStatus,
    string Title, string Priority, Guid AssigneeId) : DomainEvent;

public sealed record TaskAssignedEvent(Guid TaskId, Guid TenantId, Guid AssigneeId) : DomainEvent;

public sealed record TaskPriorityChangedEvent(
    Guid TaskId, Guid TenantId, Guid ProjectId, string OldPriority, string NewPriority,
    string Title, string Status, Guid AssigneeId) : DomainEvent;

public sealed record TaskParentChangedEvent(Guid TaskId, Guid TenantId, Guid ProjectId, Guid? OldParentTaskId, Guid? NewParentTaskId) : DomainEvent;

public sealed record TaskDependencyAddedEvent(Guid DependencyId, Guid TenantId, Guid TaskId, Guid DependsOnTaskId) : DomainEvent;

public sealed record TaskDependencyRemovedEvent(Guid DependencyId, Guid TenantId, Guid TaskId, Guid DependsOnTaskId) : DomainEvent;

public sealed record TaskAssigneeAddedEvent(Guid TaskId, Guid TenantId, Guid UserId) : DomainEvent;

public sealed record TaskAssigneeRemovedEvent(Guid TaskId, Guid TenantId, Guid UserId) : DomainEvent;

public sealed record TaskChecklistItemAddedEvent(Guid TaskId, Guid TenantId, Guid ItemId, string Text) : DomainEvent;

public sealed record TaskChecklistItemToggledEvent(Guid TaskId, Guid TenantId, Guid ItemId, bool IsDone) : DomainEvent;

public sealed record TaskChecklistItemRemovedEvent(Guid TaskId, Guid TenantId, Guid ItemId) : DomainEvent;

public sealed record TaskRecurrenceSetEvent(Guid TaskId, Guid TenantId, string Frequency, int Interval, DateOnly NextOccurrence) : DomainEvent;

public sealed record TaskRecurrenceClearedEvent(Guid TaskId, Guid TenantId) : DomainEvent;

public sealed record TaskOccurrencesGeneratedEvent(Guid TaskId, Guid TenantId, int Count, DateOnly NextOccurrence) : DomainEvent;
