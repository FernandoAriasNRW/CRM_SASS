using BuildingBlocks.Domain.Primitives;

namespace Teams.Domain.Events;

public sealed record TeamCreatedEvent(Guid TeamId, Guid TenantId, string Name) : DomainEvent;
