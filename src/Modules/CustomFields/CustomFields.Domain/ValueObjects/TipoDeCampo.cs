namespace CustomFields.Domain.ValueObjects;

/// <summary>
/// Los tipos que puede tener un campo personalizado.
///
/// **La fórmula queda fuera a propósito.** Un campo calculado necesita un motor de expresiones
/// —analizador, referencias entre campos, detección de ciclos, recálculo al cambiar un valor—
/// y eso es un proyecto en sí mismo, no un tipo más de esta lista. Prometerlo aquí a medias
/// sería peor que no tenerlo: quedaría un tipo que se puede elegir y no calcula nada.
/// </summary>
public static class TipoDeCampo
{
    public const string Texto = "Texto";
    public const string Numero = "Numero";
    public const string Fecha = "Fecha";
    public const string Seleccion = "Seleccion";
    public const string SeleccionMultiple = "SeleccionMultiple";
    public const string Usuario = "Usuario";

    public static IReadOnlyList<string> Todos() =>
        [Texto, Numero, Fecha, Seleccion, SeleccionMultiple, Usuario];

    public static bool Existe(string tipo) => Todos().Contains(tipo);

    /// <summary>Los tipos que se definen con una lista de opciones.</summary>
    public static bool UsaOpciones(string tipo) => tipo is Seleccion or SeleccionMultiple;
}
