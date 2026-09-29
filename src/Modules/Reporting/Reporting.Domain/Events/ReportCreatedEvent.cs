using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.Events;

public sealed record ReportCreatedEvent(Guid ReportId, Guid TenantId, Guid CreatedById) : DomainEvent;
