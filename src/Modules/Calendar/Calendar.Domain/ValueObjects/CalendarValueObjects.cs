using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Calendar.Domain.ValueObjects;

/// <summary>
/// Value Object para validar el título de un evento.
/// </summary>
public sealed class EventTitle : ValueObject
{
  public string Value { get; }

  private EventTitle() { Value = null!; } // EF las rellena al materializar.
  private EventTitle(string value) => Value = value;

  public static Result<EventTitle> Create(string title)
  {
    if (string.IsNullOrWhiteSpace(title))
      return Result<EventTitle>.Failure("El título es requerido");

    if (title.Length < 3)
      return Result<EventTitle>.Failure("El título debe tener al menos 3 caracteres");

    if (title.Length > 100)
      return Result<EventTitle>.Failure("El título no debe exceder los 100 caracteres");

    return Result<EventTitle>.Success(new EventTitle(title));
  }

  public override IEnumerable<object> GetEqualityComponents()
  {
    yield return Value;
  }

  // Implicit conversion para facilitar uso
  public static implicit operator string(EventTitle title) => title.Value;
}

/// <summary>
/// Enumeración para tipos de eventos de calendario.
/// </summary>
public sealed class CalendarEventType : Enumeration
{
  public static readonly CalendarEventType Meeting = new(1, "Meeting", "Reunión");
  public static readonly CalendarEventType Task = new(2, "Task", "Tarea");
  public static readonly CalendarEventType Reminder = new(3, "Reminder", "Recordatorio");
  public static readonly CalendarEventType OutOfOffice = new(4, "OutOfOffice", "Fuera de Oficina");
  public static readonly CalendarEventType Holiday = new(5, "Holiday", "Día Festivo");
  public static readonly CalendarEventType Appointment = new(6, "Appointment", "Cita");

  public string SpanishName { get; }

  private CalendarEventType() : base(0, string.Empty) { SpanishName = string.Empty; }
  private CalendarEventType(int value, string name, string spanishName) : base(value, name)
  {
    SpanishName = spanishName;
  }

  public static IReadOnlyList<CalendarEventType> All() => GetAll<CalendarEventType>();

  public static CalendarEventType? FromName(string name)
  {
    try
    {
      return GetAll<CalendarEventType>().FirstOrDefault(e =>
          e.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
          e.SpanishName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    catch
    {
      return null;
    }
  }
}

/// <summary>
/// Enumeración para patrones de recurrencia.
/// </summary>
public sealed class RecurrencePattern : Enumeration
{
  public static readonly RecurrencePattern None = new(1, "None", "Sin recurrencia");
  public static readonly RecurrencePattern Daily = new(2, "Daily", "Diario");
  public static readonly RecurrencePattern Weekly = new(3, "Weekly", "Semanal");
  public static readonly RecurrencePattern Monthly = new(4, "Monthly", "Mensual");
  public static readonly RecurrencePattern Yearly = new(5, "Yearly", "Anual");

  public string SpanishName { get; }

  private RecurrencePattern() : base(0, string.Empty) { SpanishName = string.Empty; }
  private RecurrencePattern(int value, string name, string spanishName) : base(value, name)
  {
    SpanishName = spanishName;
  }

  public static IReadOnlyList<RecurrencePattern> All() => GetAll<RecurrencePattern>();

  public static RecurrencePattern? FromName(string name)
  {
    try
    {
      return GetAll<RecurrencePattern>().FirstOrDefault(e =>
          e.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
          e.SpanishName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    catch
    {
      return null;
    }
  }
}