using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Ticketing.Domain.ValueObjects;

public sealed class TicketTitle : ValueObject
{
  public string Value { get; }

  private TicketTitle() { Value = null!; } // EF las rellena al materializar.
  private TicketTitle(string value) => Value = value;

  public static Result<TicketTitle> Create(string title)
  {
    if (string.IsNullOrWhiteSpace(title))
      return Result<TicketTitle>.Failure("Title is required");

    if (title.Length < 5)
      return Result<TicketTitle>.Failure("Title must be at least 5 characters");

    if (title.Length > 200)
      return Result<TicketTitle>.Failure("Title must not exceed 200 characters");

    return Result<TicketTitle>.Success(new TicketTitle(title));
  }

  public override IEnumerable<object> GetEqualityComponents()
  {
    yield return Value;
  }
}

public sealed class TicketPriority : Enumeration
{
  public static readonly TicketPriority Low = new(1, "Low");
  public static readonly TicketPriority Medium = new(2, "Medium");
  public static readonly TicketPriority High = new(3, "High");
  public static readonly TicketPriority Critical = new(4, "Critical");

  private TicketPriority() : base(0, string.Empty) { }
  private TicketPriority(int value, string name) : base(value, name)
  {
  }

  public static IReadOnlyList<TicketPriority> All() => GetAll<TicketPriority>();
}

public sealed class TicketStatus : Enumeration
{
  public static readonly TicketStatus Open = new(1, "Open");
  public static readonly TicketStatus InProgress = new(2, "InProgress");
  public static readonly TicketStatus PendingInfo = new(3, "PendingInfo");
  public static readonly TicketStatus Resolved = new(4, "Resolved");
  public static readonly TicketStatus Closed = new(5, "Closed");

  private TicketStatus() : base(0, string.Empty) { }
  private TicketStatus(int value, string name) : base(value, name)
  {
  }

  /// <summary>
  /// Indica si se puede pasar a otro estado. Siempre se puede.
  ///
  /// Antes había una máquina de estados en la que <c>Closed</c> era terminal. Se retiró
  /// junto con la de las tareas y por el mismo motivo: qué movimiento tiene sentido lo
  /// decide quien gestiona el trabajo, no el sistema, y en un tablero la regla se
  /// traducía en tarjetas que no se dejaban arrastrar sin explicar por qué.
  ///
  /// Reabrir un ticket cerrado es la consecuencia más visible. Algunos helpdesks lo
  /// bloquean a propósito; si aquí se quisiera bloquear, este es el único sitio donde
  /// habría que volver a decidirlo.
  ///
  /// Se conserva el método en lugar de borrarlo para no cambiar las llamadas existentes,
  /// que siguen comprobando el resultado.
  /// </summary>
  public bool CanTransitionTo(TicketStatus newStatus) => true;

  public static IReadOnlyList<TicketStatus> All() => GetAll<TicketStatus>();
}