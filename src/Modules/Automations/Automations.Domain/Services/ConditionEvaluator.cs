using Automations.Domain.Entities;
using Automations.Domain.ValueObjects;

namespace Automations.Domain.Services;

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
public static class ConditionEvaluator
{
    public static bool Matches(
        IReadOnlyCollection<AutomationCondition> conditions,
        IReadOnlyDictionary<string, string?> eventData)
    {
        return conditions.All(condition => MatchesOne(condition, eventData));
    }

    private static bool MatchesOne(
        AutomationCondition condition,
        IReadOnlyDictionary<string, string?> eventData)
    {
        // Un campo que el disparador no trae no es «vacío»: es «no aplica». Tratarlo como vacío
        // haría que una regla pensada para otro disparador se ejecutara por accidente.
        if (!eventData.TryGetValue(condition.Field, out var eventValue))
            return false;

        var expected = condition.Value ?? string.Empty;

        return condition.Operator switch
        {
            ValueObjects.ConditionOperators.EqualTo =>
                string.Equals(eventValue, expected, StringComparison.OrdinalIgnoreCase),

            ValueObjects.ConditionOperators.NotEqualTo =>
                !string.Equals(eventValue, expected, StringComparison.OrdinalIgnoreCase),

            ValueObjects.ConditionOperators.Contains =>
                eventValue is not null
                && eventValue.Contains(expected, StringComparison.OrdinalIgnoreCase),

            ValueObjects.ConditionOperators.IsEmpty => string.IsNullOrWhiteSpace(eventValue),

            // Comparación numérica de verdad, no alfabética. El dominio ya impide guardar estos
            // operadores sobre un campo de texto, así que aquí basta con que los dos lados sean
            // números; si alguno no lo es —un dato corrupto—, la condición no se cumple, que es
            // el lado seguro: no tocar datos de nadie ante la duda.
            ValueObjects.ConditionOperators.LessOrEqual =>
                AreNumbers(eventValue, expected, out var a, out var b) && a <= b,

            ValueObjects.ConditionOperators.GreaterOrEqual =>
                AreNumbers(eventValue, expected, out var c, out var d) && c >= d,

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
    private static bool AreNumbers(string? left, string right, out int a, out int b)
    {
        b = 0;
        return int.TryParse(left, System.Globalization.NumberStyles.Integer,
                   System.Globalization.CultureInfo.InvariantCulture, out a)
            && int.TryParse(right, System.Globalization.NumberStyles.Integer,
                   System.Globalization.CultureInfo.InvariantCulture, out b);
    }
}
