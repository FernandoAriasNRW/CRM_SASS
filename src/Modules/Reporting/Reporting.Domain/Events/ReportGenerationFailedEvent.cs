using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.Events;

public sealed record ReportGenerationFailedEvent(Guid ReportId, Guid TenantId, string Error) : DomainEvent;
