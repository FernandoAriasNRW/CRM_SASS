namespace WorkItems.Domain.ValueObjects;

/// <summary>
/// Cada cuánto se repite una tarea.
///
/// Se guarda el patrón, no la lista de fechas futuras: generar de antemano un año de
/// ocurrencias llenaría el tablero de tareas que nadie ha mirado todavía, y cambiar la
/// periodicidad obligaría a salir a borrarlas una por una.
/// </summary>
public sealed class RecurrencePattern
{
    public const int MaxInterval = 365;

    /// <summary>Diaria, Semanal o Mensual.</summary>
    public string Frequency { get; private set; } = string.Empty;

    /// <summary>Cada cuántas unidades de la frecuencia. 2 + Semanal es «cada dos semanas».</summary>
    public int Interval { get; private set; }

    /// <summary>Fecha de la próxima tarea que toca crear.</summary>
    public DateOnly NextOccurrence { get; private set; }

    /// <summary>Cuándo deja de repetirse. Sin fecha, no deja de repetirse.</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>
    /// Día del mes con el que nació la serie, que se guarda porque las mensuales lo necesitan.
    ///
    /// Una serie que empieza el 31 y pasa por febrero cae el 28 ese mes, pero tiene que volver
    /// al 31 en marzo. Si la siguiente fecha se calculara desde la última —ya recortada—, la
    /// serie se degradaría al 28 para siempre.
    /// </summary>
    public int SeriesDay { get; private set; }

    private RecurrencePattern() { }

    public RecurrencePattern(string frequency, int interval, DateOnly nextOccurrence, DateOnly? endDate)
    {
        if (!Frequencies.Exists(frequency))
            throw new InvalidOperationException(Rules.UnknownFrequency);

        if (interval < 1 || interval > MaxInterval)
            throw new InvalidOperationException(Rules.IntervalOutOfRange);

        if (endDate.HasValue && endDate.Value < nextOccurrence)
            throw new InvalidOperationException(Rules.EndBeforeStart);

        Frequency = frequency;
        Interval = interval;
        NextOccurrence = nextOccurrence;
        EndDate = endDate;
        SeriesDay = nextOccurrence.Day;
    }

    /// <summary>Si todavía queda alguna ocurrencia por crear a fecha de <paramref name="today"/>.</summary>
    public bool IsDue(DateOnly today)
        => NextOccurrence <= today && (!EndDate.HasValue || NextOccurrence <= EndDate.Value);

    /// <summary>Si el patrón ya no dará más tareas.</summary>
    public bool IsExhausted => EndDate.HasValue && NextOccurrence > EndDate.Value;

    internal void AdvanceTo(DateOnly next) => NextOccurrence = next;

    public static class Frequencies
    {
        public const string Daily = "Daily";
        public const string Weekly = "Weekly";
        public const string Monthly = "Monthly";

        public static IReadOnlyList<string> All() => [Daily, Weekly, Monthly];

        public static bool Exists(string frequency) => All().Contains(frequency);
    }

    public static class Rules
    {
        public const string UnknownFrequency = "La frecuencia debe ser Daily, Weekly o Monthly";
        public static readonly string IntervalOutOfRange =
            $"El intervalo tiene que estar entre 1 y {MaxInterval}";
        public const string EndBeforeStart = "La fecha de fin no puede ser anterior a la próxima ocurrencia";
    }
}
