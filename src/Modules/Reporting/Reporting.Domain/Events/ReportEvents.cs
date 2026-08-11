using BuildingBlocks.Domain.Primitives;

namespace Reporting.Domain.Events;

public sealed record ReportCreatedEvent(Guid ReportId, Guid TenantId, Guid CreatedById) : DomainEvent;
public sealed record ReportGeneratedEvent(Guid ReportId, Guid TenantId, string FileUrl) : DomainEvent;
public sealed record ReportGenerationFailedEvent(Guid ReportId, Guid TenantId, string Error) : DomainEvent;

