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

  /// <summary>
  /// El ticket con el que está enlazado, si lo está.
  ///
  /// Faltaba: había enlace a proyecto y a tarea pero no a ticket, y el ticket es justamente el
  /// diferencial de este producto. Una llamada de seguimiento con un cliente cuelga de su ticket,
  /// no de una tarea interna.
  /// </summary>
  public Guid? TicketId { get; private set; }
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

  /// <summary>
  /// Cuándo se canceló, o nulo si sigue en pie.
  ///
  /// <b>Cancelar y tirar a la papelera no son lo mismo, y hasta ahora sí lo eran.</b> Un evento
  /// cancelado <b>se sigue viendo</b>, tachado: quien mira el jueves necesita saber que la reunión
  /// se anuló, no que nunca existió —si desaparece, la gente se presenta igual—. La papelera es
  /// para lo que no debería estar ahí, un evento creado por error.
  ///
  /// Por eso son dos marcas y no un estado: un evento puede estar cancelado y además acabar en la
  /// papelera, y el orden en que pase no cambia lo que significa cada cosa.
  /// </summary>
  public DateTime? CanceladoEnUtc { get; private set; }

  public Guid? CanceladoPor { get; private set; }

  /// <summary>Por qué se canceló. Se enseña junto al evento tachado.</summary>
  public string? MotivoDeCancelacion { get; private set; }

  public bool EstaCancelado => CanceladoEnUtc is not null;

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
      Guid? ticketId = null,
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
      TicketId = ticketId,
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

  /// <summary>
  /// Manda el evento a la papelera: deja de verse, y se puede recuperar.
  ///
  /// Antes se llamaba <c>Cancel</c> y hacía esto mismo, de modo que cancelar una reunión la hacía
  /// desaparecer del calendario. Ver <see cref="CanceladoEnUtc"/> para por qué son cosas distintas.
  /// </summary>
  public void EnviarAPapelera(Guid porQuien)
  {
    if (IsDeleted)
      throw new InvalidOperationException("El evento ya está en la papelera");

    IsDeleted = true;
    DeletedAt = DateTime.UtcNow;
    DeletedBy = porQuien;

    RaiseDomainEvent(new CalendarCancelledEvent(Id, TenantId, porQuien));
  }

  /// <summary>
  /// Anula el evento sin quitarlo del calendario.
  ///
  /// Se permite cancelar un evento ya pasado a propósito: a veces se anota después de que la
  /// reunión no llegara a celebrarse, y prohibirlo obligaría a borrarla, que es peor —quedaría
  /// como si se hubiera hecho—.
  /// </summary>
  public Result<CalendarEvent> Cancelar(Guid porQuien, string? motivo = null)
  {
    if (IsDeleted)
      return Result<CalendarEvent>.Failure("No se puede cancelar un evento que está en la papelera");

    if (EstaCancelado)
      return Result<CalendarEvent>.Failure("El evento ya está cancelado");

    CanceladoEnUtc = DateTime.UtcNow;
    CanceladoPor = porQuien;
    MotivoDeCancelacion = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();

    RaiseDomainEvent(new EventoCanceladoEvent(Id, TenantId, porQuien, MotivoDeCancelacion));
    return Result<CalendarEvent>.Success(this);
  }

  /// <summary>Deshace la cancelación: la reunión vuelve a estar en pie.</summary>
  public Result<CalendarEvent> Reactivar()
  {
    if (!EstaCancelado)
      return Result<CalendarEvent>.Failure("El evento no está cancelado");

    CanceladoEnUtc = null;
    CanceladoPor = null;
    MotivoDeCancelacion = null;

    return Result<CalendarEvent>.Success(this);
  }

  /// <summary>
  /// Enlaza el evento con un proyecto, una tarea o un ticket —o quita el enlace pasando nulo.
  ///
  /// Los tres a la vez están permitidos: una reunión puede ser sobre un ticket dentro de un
  /// proyecto, y obligar a elegir uno haría que la información se perdiera en la descripción,
  /// donde no la encuentra ninguna consulta.
  /// </summary>
  public Result<CalendarEvent> Enlazar(Guid? proyectoId, Guid? tareaId, Guid? ticketId)
  {
    if (IsDeleted)
      return Result<CalendarEvent>.Failure("No se puede enlazar un evento que está en la papelera");

    ProjectId = proyectoId;
    TaskId = tareaId;
    TicketId = ticketId;

    return Result<CalendarEvent>.Success(this);
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
