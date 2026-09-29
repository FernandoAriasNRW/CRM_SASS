using BuildingBlocks.Domain.Primitives;

namespace CustomFields.Domain.Events;

public sealed record CustomFieldDefinedEvent(Guid DefinitionId, Guid TenantId, string Name, string Type, string TargetEntity) : DomainEvent;

public sealed record CustomFieldUpdatedEvent(Guid DefinitionId, Guid TenantId, string Name) : DomainEvent;

public sealed record CustomFieldRemovedEvent(Guid DefinitionId, Guid TenantId) : DomainEvent;

public sealed record CustomFieldValueSetEvent(Guid ValueId, Guid TenantId, Guid DefinitionId, Guid EntityId) : DomainEvent;
