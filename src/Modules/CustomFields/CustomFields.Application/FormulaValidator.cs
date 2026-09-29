using CustomFields.Application.Abstractions;
using CustomFields.Domain.Entities;
using CustomFields.Domain.Services;
using CustomFields.Domain.ValueObjects;

namespace CustomFields.Application;

/// <summary>
/// Comprueba una fórmula contra los demás campos del inquilino.
///
/// Vive en la capa de aplicación y no en el dominio porque necesita **ver a los vecinos**: que
/// una referencia apunte a un campo que existe, que ese campo sirva como número y que el
/// conjunto no forme un ciclo son cosas que no se pueden saber mirando sólo la definición que
/// se está guardando. El dominio comprueba lo que puede comprobar solo —que la expresión se
/// pueda leer— y aquí se hace el resto.
///
/// Todo esto ocurre **al guardar**, en el formulario, donde quien escribió la fórmula puede
/// corregirla. Descubrir un ciclo al abrir una tarea tres semanas después no le sirve a nadie.
/// </summary>
public static class FormulaValidator
{
    /// <summary>
    /// Devuelve el problema, o <c>null</c> si la fórmula es válida.
    ///
    /// <paramref name="editedId"/> es el campo que se está modificando, para excluirlo del
    /// grafo: si se dejara, editar una fórmula se compararía contra su propia versión anterior
    /// y daría ciclos donde no los hay.
    /// </summary>
    public static async Task<string?> ValidateAgainstOthersAsync(
        ICustomFieldRepository repository,
        Guid tenantId,
        string targetEntity,
        string name,
        string? formula,
        Guid? editedId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return null;   // no es un campo calculado, o el dominio ya lo habrá rechazado

        var analysis = FormulaParser.Parse(formula);
        if (!analysis.IsValid)
            return analysis.Error;

        var references = FormulaParser.ReferencesOf(analysis.Tree!);

        // Sólo los campos de la misma entidad. Una fórmula de un campo de tarea no puede usar
        // un campo de proyecto: no hay una tarea con un solo proyecto en el que apoyarse desde
        // aquí, y fingir que la hay daría resultados según qué proyecto tocara.
        var neighbours = (await repository.GetDefinitionsAsync(tenantId, targetEntity, ct))
            .Where(d => editedId is null || d.Id != editedId)
            .ToList();

        var byName = neighbours.ToDictionary(
            d => d.Name, d => d, FormulaCycleDetector.NameComparer);

        foreach (var reference in references)
        {
            if (!byName.TryGetValue(reference, out var field))
                return $"La fórmula usa el campo «{reference}», que no existe en {targetEntity}";

            if (!FieldType.UsableInFormula(field.Type))
                return $"«{reference}» es de tipo {field.Type}. "
                     + CustomFieldDefinition.Rules.NonNumericReference;
        }

        var existingFields = neighbours
            .Where(d => FieldType.IsComputed(d.Type) && d.Formula is not null)
            .Select(d =>
            {
                var own = FormulaParser.Parse(d.Formula);

                // Una fórmula ya guardada que no se puede leer no debería existir, pero si la
                // hay se cuenta como sin referencias en vez de reventar aquí: el problema es de
                // ese campo, y no tiene por qué impedir guardar éste.
                return new FormulaCycleDetector.Dependency(
                    d.Name,
                    own.IsValid ? FormulaParser.ReferencesOf(own.Tree!) : []);
            });

        return FormulaCycleDetector.WouldCreateCycle(existingFields, name.Trim(), references)
            ? CustomFieldDefinition.Rules.FormulaCycle
            : null;
    }
}
