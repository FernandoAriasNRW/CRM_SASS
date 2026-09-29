using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.Events;

public sealed record ReportGeneratedEvent(Guid ReportId, Guid TenantId, string FileUrl) : DomainEvent;
