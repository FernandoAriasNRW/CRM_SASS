using BuildingBlocks.Domain.Primitives;

namespace CustomFields.Domain.Events;

public sealed record CustomFieldDefinedEvent(Guid DefinitionId, Guid TenantId, string Nombre, string Tipo, string EntidadDestino) : DomainEvent;

public sealed record CustomFieldUpdatedEvent(Guid DefinitionId, Guid TenantId, string Nombre) : DomainEvent;

public sealed record CustomFieldRemovedEvent(Guid DefinitionId, Guid TenantId) : DomainEvent;

public sealed record CustomFieldValueSetEvent(Guid ValueId, Guid TenantId, Guid DefinitionId, Guid EntityId) : DomainEvent;
