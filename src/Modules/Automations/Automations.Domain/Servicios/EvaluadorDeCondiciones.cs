using Automations.Domain.Entities;
using Automations.Domain.ValueObjects;

namespace Automations.Domain.Servicios;

/// <summary>
/// Decide si un evento cumple las condiciones de una regla.
///
/// Es una **función pura** por el mismo motivo que el detector de ciclos o el calendario de
/// recurrencia: equivocarse aquí no da error, hace que una automatización se ejecute cuando no
/// debía —o que no se ejecute y nadie se entere—, y las dos cosas se descubren tarde y a mano.
/// Sin base de datos se puede recorrer toda la combinatoria.
///
/// **Las condiciones se combinan con Y.** Un «o» exige agrupar y precedencias, que es un
/// lenguaje de expresiones; quien necesite un «o» crea dos reglas, que además se leen mejor en
/// una lista. Sin condiciones, la regla se aplica siempre que salte su disparador.
/// </summary>
public static class EvaluadorDeCondiciones
{
    public static bool Cumple(
        IReadOnlyCollection<CondicionDeAutomatizacion> condiciones,
        IReadOnlyDictionary<string, string?> datosDelEvento)
    {
        return condiciones.All(condicion => CumpleUna(condicion, datosDelEvento));
    }

    private static bool CumpleUna(
        CondicionDeAutomatizacion condicion,
        IReadOnlyDictionary<string, string?> datosDelEvento)
    {
        // Un campo que el disparador no trae no es «vacío»: es «no aplica». Tratarlo como vacío
        // haría que una regla pensada para otro disparador se ejecutara por accidente.
        if (!datosDelEvento.TryGetValue(condicion.Campo, out var valorDelEvento))
            return false;

        var esperado = condicion.Valor ?? string.Empty;

        return condicion.Operador switch
        {
            ValueObjects.Operador.Igual =>
                string.Equals(valorDelEvento, esperado, StringComparison.OrdinalIgnoreCase),

            ValueObjects.Operador.Distinto =>
                !string.Equals(valorDelEvento, esperado, StringComparison.OrdinalIgnoreCase),

            ValueObjects.Operador.Contiene =>
                valorDelEvento is not null
                && valorDelEvento.Contains(esperado, StringComparison.OrdinalIgnoreCase),

            ValueObjects.Operador.EstaVacio => string.IsNullOrWhiteSpace(valorDelEvento),

            // Comparación numérica de verdad, no alfabética. El dominio ya impide guardar estos
            // operadores sobre un campo de texto, así que aquí basta con que los dos lados sean
            // números; si alguno no lo es —un dato corrupto—, la condición no se cumple, que es
            // el lado seguro: no tocar datos de nadie ante la duda.
            ValueObjects.Operador.MenorOIgual =>
                SonNumeros(valorDelEvento, esperado, out var a, out var b) && a <= b,

            ValueObjects.Operador.MayorOIgual =>
                SonNumeros(valorDelEvento, esperado, out var c, out var d) && c >= d,

            // Un operador que esta versión no conoce no se cumple. Ejecutar la acción ante la
            // duda sería tocar datos de alguien por un dato que no se entiende.
            _ => false,
        };
    }

    /// <summary>
    /// Cultura invariante en los dos lados: el valor del evento lo escribe el sistema y el
    /// esperado lo escribió una persona, y si cada uno se leyera con su cultura una regla
    /// funcionaría o no según el idioma del servidor.
    /// </summary>
    private static bool SonNumeros(string? izquierda, string derecha, out int a, out int b)
    {
        b = 0;
        return int.TryParse(izquierda, System.Globalization.NumberStyles.Integer,
                   System.Globalization.CultureInfo.InvariantCulture, out a)
            && int.TryParse(derecha, System.Globalization.NumberStyles.Integer,
                   System.Globalization.CultureInfo.InvariantCulture, out b);
    }
}
