namespace WorkItems.Domain.ValueObjects;

/// <summary>
/// Cada cuánto se repite una tarea.
///
/// Se guarda el patrón, no la lista de fechas futuras: generar de antemano un año de
/// ocurrencias llenaría el tablero de tareas que nadie ha mirado todavía, y cambiar la
/// periodicidad obligaría a salir a borrarlas una por una.
/// </summary>
public sealed class PatronDeRecurrencia
{
    public const int IntervaloMaximo = 365;

    /// <summary>Diaria, Semanal o Mensual.</summary>
    public string Frecuencia { get; private set; } = string.Empty;

    /// <summary>Cada cuántas unidades de la frecuencia. 2 + Semanal es «cada dos semanas».</summary>
    public int Intervalo { get; private set; }

    /// <summary>Fecha de la próxima tarea que toca crear.</summary>
    public DateOnly ProximaOcurrencia { get; private set; }

    /// <summary>Cuándo deja de repetirse. Sin fecha, no deja de repetirse.</summary>
    public DateOnly? FechaFin { get; private set; }

    /// <summary>
    /// Día del mes con el que nació la serie, que se guarda porque las mensuales lo necesitan.
    ///
    /// Una serie que empieza el 31 y pasa por febrero cae el 28 ese mes, pero tiene que volver
    /// al 31 en marzo. Si la siguiente fecha se calculara desde la última —ya recortada—, la
    /// serie se degradaría al 28 para siempre.
    /// </summary>
    public int DiaDeLaSerie { get; private set; }

    private PatronDeRecurrencia() { }

    public PatronDeRecurrencia(string frecuencia, int intervalo, DateOnly proximaOcurrencia, DateOnly? fechaFin)
    {
        if (!Frecuencias.Existe(frecuencia))
            throw new InvalidOperationException(Reglas.FrecuenciaDesconocida);

        if (intervalo < 1 || intervalo > IntervaloMaximo)
            throw new InvalidOperationException(Reglas.IntervaloFueraDeRango);

        if (fechaFin.HasValue && fechaFin.Value < proximaOcurrencia)
            throw new InvalidOperationException(Reglas.FinAntesDelPrincipio);

        Frecuencia = frecuencia;
        Intervalo = intervalo;
        ProximaOcurrencia = proximaOcurrencia;
        FechaFin = fechaFin;
        DiaDeLaSerie = proximaOcurrencia.Day;
    }

    /// <summary>Si todavía queda alguna ocurrencia por crear a fecha de <paramref name="hoy"/>.</summary>
    public bool TocaGenerar(DateOnly hoy)
        => ProximaOcurrencia <= hoy && (!FechaFin.HasValue || ProximaOcurrencia <= FechaFin.Value);

    /// <summary>Si el patrón ya no dará más tareas.</summary>
    public bool Agotado => FechaFin.HasValue && ProximaOcurrencia > FechaFin.Value;

    internal void AvanzarA(DateOnly siguiente) => ProximaOcurrencia = siguiente;

    public static class Frecuencias
    {
        public const string Diaria = "Diaria";
        public const string Semanal = "Semanal";
        public const string Mensual = "Mensual";

        public static IReadOnlyList<string> Todas() => [Diaria, Semanal, Mensual];

        public static bool Existe(string frecuencia) => Todas().Contains(frecuencia);
    }

    public static class Reglas
    {
        public const string FrecuenciaDesconocida = "La frecuencia debe ser Diaria, Semanal o Mensual";
        public static readonly string IntervaloFueraDeRango =
            $"El intervalo tiene que estar entre 1 y {IntervaloMaximo}";
        public const string FinAntesDelPrincipio = "La fecha de fin no puede ser anterior a la próxima ocurrencia";
    }
}
