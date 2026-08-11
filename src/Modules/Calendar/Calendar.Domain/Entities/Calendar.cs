using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Calendar.Domain.Events;
using Calendar.Domain.ValueObjects;

namespace Calendar.Domain.Entities;

public sealed class CalendarEvent : AggregateRoot, ITenantEntity, ISoftDeletable
{
  public Guid TenantId { get; private set; }
  public Guid OrganizerId { get; private set; }
  public Guid? ProjectId { get; private set; }
  public Guid? TaskId { get; private set; }
  public string Title { get; private set; } = string.Empty;
  public string? Description { get; private set; }
  public int TypeValue { get; private set; }
  public DateTime StartTime { get; private set; }
  public DateTime EndTime { get; private set; }
  public string? Location { get; private set; }
  public bool IsAllDay { get; private set; }
  public int RecurrenceValue { get; private set; }
  public int? RecurrenceInterval { get; private set; }
  public DateTime? RecurrenceEndDate { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public bool IsDeleted { get; private set; }
  public DateTime? DeletedAt { get; private set; }
  public Guid? DeletedBy { get; private set; }

  public CalendarEventType Type => CalendarEventType.FromValue<CalendarEventType>(TypeValue);
  public RecurrencePattern Recurrence => RecurrencePattern.FromValue<RecurrencePattern>(RecurrenceValue);

  private CalendarEvent()
  { }

  public static Result<CalendarEvent> Create(
      Guid tenantId,
      Guid organizerId,
      string title,
      DateTime startTime,
      DateTime endTime,
      CalendarEventType type,
      Guid? projectId = null,
      Guid? taskId = null,
      string? description = null,
      string? location = null,
      bool isAllDay = false,
      RecurrencePattern recurrence = null!)
  {
    var titleResult = EventTitle.Create(title);
    if (titleResult.IsFailure)
      return Result<CalendarEvent>.Failure(titleResult.Error!);

    if (endTime <= startTime)
      return Result<CalendarEvent>.Failure("End time must be after start time");

    var evt = new CalendarEvent
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      OrganizerId = organizerId,
      ProjectId = projectId,
      TaskId = taskId,
      Title = title,
      Description = description,
      TypeValue = type.Value,
      StartTime = startTime,
      EndTime = endTime,
      Location = location,
      IsAllDay = isAllDay,
      RecurrenceValue = (recurrence ?? RecurrencePattern.None).Value,
      CreatedAt = DateTime.UtcNow
    };

    evt.RaiseDomainEvent(new CalendarCreatedEvent(evt.Id, tenantId, organizerId, title));
    return Result<CalendarEvent>.Success(evt);
  }

  public Result<CalendarEvent> Reschedule(DateTime newStartTime, DateTime newEndTime)
  {
    if (newEndTime <= newStartTime)
      return Result<CalendarEvent>.Failure("End time must be after start time");

    StartTime = newStartTime;
    EndTime = newEndTime;
    RaiseDomainEvent(new CalendarRescheduledEvent(Id, TenantId, newStartTime, newEndTime));
    return Result<CalendarEvent>.Success(this);
  }

  public void Cancel(Guid deletedBy)
  {
    if (IsDeleted)
      throw new InvalidOperationException("The calendar event is already cancelled");

    IsDeleted = true;
    DeletedAt = DateTime.UtcNow;
    DeletedBy = deletedBy;

    RaiseDomainEvent(new CalendarCancelledEvent(Id, TenantId, deletedBy));
  }

  public Result<CalendarEvent> Update(string? title, DateTime? startTime, DateTime? endTime, string? description, string? location)
  {
    if (IsDeleted)
      return Result<CalendarEvent>.Failure("Cannot update a deleted calendar event");

    if (!string.IsNullOrWhiteSpace(title))
    {
      var titleResult = EventTitle.Create(title);
      if (titleResult.IsFailure)
        return Result<CalendarEvent>.Failure(titleResult.Error!);

      Title = title;
    }

    var nextStartTime = startTime ?? StartTime;
    var nextEndTime = endTime ?? EndTime;
    if (nextEndTime <= nextStartTime)
      return Result<CalendarEvent>.Failure("End time must be after start time");

    StartTime = nextStartTime;
    EndTime = nextEndTime;

    if (description is not null)
      Description = description;

    if (location is not null)
      Location = location;

    RaiseDomainEvent(new CalendarUpdatedEvent(Id, TenantId, title, startTime, endTime, description, location));
    return Result<CalendarEvent>.Success(this);
  }

  public void Restore()
  {
    if (!IsDeleted)
      throw new InvalidOperationException("The calendar event is not deleted");

    IsDeleted = false;
    DeletedAt = null;
    DeletedBy = null;
  }
}
