namespace BuildingBlocks.Contracts.IntegrationEvents;

public sealed record ProjectCreatedEvent(Guid ProjectId, Guid TenantId, string Name);
public sealed record ProjectUpdatedEvent(Guid ProjectId, Guid TenantId, string Name, string Status);
public sealed record TaskCreatedEvent(Guid TaskId, Guid ProjectId, Guid TenantId, Guid AssigneeId);
public sealed record TaskStatusChangedEvent(Guid TaskId, Guid TenantId, string OldStatus, string NewStatus);
public sealed record TicketCreatedEvent(Guid TicketId, Guid TenantId, string Subject, string ContactEmail);
public sealed record TicketAssignedEvent(Guid TicketId, Guid TenantId, Guid AssignedToId);
public sealed record NotificationRequestedEvent(Guid UserId, string Title, string Body);
public sealed record CalendarEventRequestedEvent(Guid TenantId, Guid CreatedById, string Title, string Description, DateTime StartsAtUtc, DateTime EndsAtUtc);
