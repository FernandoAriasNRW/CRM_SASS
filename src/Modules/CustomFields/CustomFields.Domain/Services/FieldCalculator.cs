using System.Globalization;
using CustomFields.Domain.Entities;
using CustomFields.Domain.ValueObjects;

namespace CustomFields.Domain.Services;

/// <summary>
/// Calcula todos los campos con fórmula de una entidad, de una vez y en el orden correcto.
///
/// Función pura: entran las definiciones y los valores guardados, sale qué vale cada campo
/// calculado. Ni base de datos ni peticiones, así que se puede probar con formas de grafo que en
/// producción tardarían meses en aparecer.
///
/// **El orden importa y no es el de la pantalla.** Un campo calculado puede usar otro campo
/// calculado —«Margen» sobre «Total», que a su vez sale de «Horas» por «Precio»—, y evaluarlos
/// en el orden en que se muestran daría hueco en el que va primero. Se ordenan por dependencia
/// con <see cref="FormulaCycleDetector.ComputationOrder"/>.
/// </summary>
public static class FieldCalculator
{
    /// <summary>Lo que vale un campo calculado, y por qué si no vale nada.</summary>
    public sealed record Computed(Guid DefinitionId, string Name, string? Value, string? Error);

    /// <summary>
    /// Calcula los campos con fórmula.
    ///
    /// <paramref name="storedValues"/> son los valores tal como están en la base, indexados
    /// por definición: sólo los de los campos que se rellenan a mano. Los calculados no tienen
    /// valor guardado — por eso son calculados.
    /// </summary>
    public static IReadOnlyList<Computed> Compute(
        IEnumerable<CustomFieldDefinition> definitions,
        IReadOnlyDictionary<Guid, string?> storedValues)
    {
        var all = definitions.ToList();
        var withFormula = all.Where(d => FieldType.IsComputed(d.Type) && d.Formula is not null).ToList();

        if (withFormula.Count == 0)
            return [];

        var comparador = FormulaCycleDetector.NameComparer;

        // Los campos que se rellenan a mano y sirven como número. Un campo de texto que
        // «parece un número» no entra: haría que la fórmula funcionara según lo que alguien
        // tecleara ese día, y el fallo saldría como un hueco sin explicación.
        var numbers = new Dictionary<string, decimal?>(comparador);
        foreach (var d in all.Where(d => d.Type == FieldType.Number))
        {
            var text = storedValues.TryGetValue(d.Id, out var v) ? v : null;

            numbers[d.Name] = decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
                ? n
                : null;   // sin rellenar, o ilegible: hueco
        }

        // Se analizan una vez, no una por cada referencia.
        var trees = new Dictionary<string, FormulaParser.Node>(comparador);
        var definitionByName = new Dictionary<string, CustomFieldDefinition>(comparador);
        var errors = new Dictionary<string, string>(comparador);

        foreach (var d in withFormula)
        {
            definitionByName[d.Name] = d;

            var analysis = FormulaParser.Parse(d.Formula);

            if (analysis.IsValid)
                trees[d.Name] = analysis.Tree!;
            else
                // Una fórmula guardada que ya no se puede leer sólo debería ocurrir si alguien
                // tocó la base a mano o si cambió el lenguaje. Se informa por campo en vez de
                // reventar el formulario entero: el resto de campos siguen siendo útiles.
                errors[d.Name] = analysis.Error!;
        }

        var dependencies = trees.Select(pair =>
            new FormulaCycleDetector.Dependency(pair.Key, FormulaParser.ReferencesOf(pair.Value)));

        var order = FormulaCycleDetector.ComputationOrder(dependencies);

        // Un ciclo aquí significa que se guardaron fórmulas que la comprobación de alta debería
        // haber rechazado. No se intenta evaluar ninguna: la alternativa sería colgarse.
        if (order is null)
        {
            return withFormula
                .Select(d => new Computed(d.Id, d.Name, null, CustomFieldDefinition.Rules.FormulaCycle))
                .ToList();
        }

        var computedValues = new Dictionary<string, decimal?>(comparador);
        var results = new List<Computed>();

        foreach (var name in order)
        {
            if (!trees.TryGetValue(name, out var tree))
                continue;   // es un campo normal que aparece en el orden por ser dependencia

            var result = FormulaEvaluator.Evaluate(tree, reference =>
            {
                if (computedValues.TryGetValue(reference, out var alreadyComputed))
                    return alreadyComputed;

                if (numbers.TryGetValue(reference, out var number))
                    return number;

                // Referencia a un campo que existe pero no sirve en una fórmula —un texto, una
                // fecha—, o a uno que no existe. En ambos casos es un error de la fórmula, no un
                // hueco: quien la escribió tiene que arreglarla.
                throw new FormulaEvaluator.UnknownFieldException(reference);
            });

            computedValues[name] = result.Value;

            var definition = definitionByName[name];
            results.Add(new Computed(definition.Id, name, result.Text, result.Error));
        }

        // Las que no se pudieron ni analizar no entraron en el orden; salen igualmente, con su
        // error, para que la pantalla pueda decir qué pasa en lugar de no mostrar el campo.
        foreach (var (name, error) in errors)
        {
            var definition = definitionByName[name];
            results.Add(new Computed(definition.Id, name, null, error));
        }

        return results;
    }
}
