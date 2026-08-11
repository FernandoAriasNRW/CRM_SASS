using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Ticketing.Domain.Events;

public sealed record TicketCreatedEvent(Guid TicketId, Guid TenantId) : DomainEvent;
public sealed record TicketStatusChangedEvent(Guid TicketId, Guid TenantId, int PreviousStatus, int NewStatus) : DomainEvent;
public sealed record TicketAssignedEvent(Guid TicketId, Guid TenantId, Guid AgentId) : DomainEvent;
public sealed record TicketUnassignedEvent(Guid TicketId, Guid TenantId) : DomainEvent;

