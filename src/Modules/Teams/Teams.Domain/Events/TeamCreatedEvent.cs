using BuildingBlocks.Domain.Primitives;

namespace Teams.Domain.Events;

public sealed record TeamCreatedEvent(Guid TeamId, Guid TenantId, string Name) : DomainEvent;

/// <summary>Personas que entran en un equipo o salen de él.</summary>
public sealed record TeamMembersChangedEvent(
    Guid TeamId, Guid TenantId, string Name, IReadOnlyList<Guid> Added, IReadOnlyList<Guid> Removed) : DomainEvent;
