using CustomFields.Application.Abstractions;
using CustomFields.Domain.Entities;
using CustomFields.Domain.Servicios;
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
public static class ValidarFormula
{
    /// <summary>
    /// Devuelve el problema, o <c>null</c> si la fórmula es válida.
    ///
    /// <paramref name="idQueSeEdita"/> es el campo que se está modificando, para excluirlo del
    /// grafo: si se dejara, editar una fórmula se compararía contra su propia versión anterior
    /// y daría ciclos donde no los hay.
    /// </summary>
    public static async Task<string?> ContraLosDemasAsync(
        ICustomFieldRepository repositorio,
        Guid tenantId,
        string entidadDestino,
        string nombre,
        string? formula,
        Guid? idQueSeEdita,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return null;   // no es un campo calculado, o el dominio ya lo habrá rechazado

        var analisis = AnalizadorDeFormula.Analizar(formula);
        if (!analisis.EsValida)
            return analisis.Error;

        var referencias = AnalizadorDeFormula.ReferenciasDe(analisis.Arbol!);

        // Sólo los campos de la misma entidad. Una fórmula de un campo de tarea no puede usar
        // un campo de proyecto: no hay una tarea con un solo proyecto en el que apoyarse desde
        // aquí, y fingir que la hay daría resultados según qué proyecto tocara.
        var vecinos = (await repositorio.GetDefinitionsAsync(tenantId, entidadDestino, ct))
            .Where(d => idQueSeEdita is null || d.Id != idQueSeEdita)
            .ToList();

        var porNombre = vecinos.ToDictionary(
            d => d.Nombre, d => d, DetectorDeCiclosDeFormula.ComparadorDeNombres);

        foreach (var referencia in referencias)
        {
            if (!porNombre.TryGetValue(referencia, out var campo))
                return $"La fórmula usa el campo «{referencia}», que no existe en {entidadDestino}";

            if (!TipoDeCampo.SirveEnFormula(campo.Tipo))
                return $"«{referencia}» es de tipo {campo.Tipo}. "
                     + CustomFieldDefinition.Reglas.ReferenciaNoNumerica;
        }

        var existentes = vecinos
            .Where(d => TipoDeCampo.SeCalcula(d.Tipo) && d.Formula is not null)
            .Select(d =>
            {
                var suyo = AnalizadorDeFormula.Analizar(d.Formula);

                // Una fórmula ya guardada que no se puede leer no debería existir, pero si la
                // hay se cuenta como sin referencias en vez de reventar aquí: el problema es de
                // ese campo, y no tiene por qué impedir guardar éste.
                return new DetectorDeCiclosDeFormula.Dependencia(
                    d.Nombre,
                    suyo.EsValida ? AnalizadorDeFormula.ReferenciasDe(suyo.Arbol!) : []);
            });

        return DetectorDeCiclosDeFormula.CerrariaUnCiclo(existentes, nombre.Trim(), referencias)
            ? CustomFieldDefinition.Reglas.FormulaEnCiclo
            : null;
    }
}
