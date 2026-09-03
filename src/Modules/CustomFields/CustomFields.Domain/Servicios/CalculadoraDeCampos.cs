using System.Globalization;
using CustomFields.Domain.Entities;
using CustomFields.Domain.ValueObjects;

namespace CustomFields.Domain.Servicios;

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
/// con <see cref="DetectorDeCiclosDeFormula.OrdenDeCalculo"/>.
/// </summary>
public static class CalculadoraDeCampos
{
    /// <summary>Lo que vale un campo calculado, y por qué si no vale nada.</summary>
    public sealed record Calculado(Guid DefinitionId, string Nombre, string? Valor, string? Error);

    /// <summary>
    /// Calcula los campos con fórmula.
    ///
    /// <paramref name="valoresGuardados"/> son los valores tal como están en la base, indexados
    /// por definición: sólo los de los campos que se rellenan a mano. Los calculados no tienen
    /// valor guardado — por eso son calculados.
    /// </summary>
    public static IReadOnlyList<Calculado> Calcular(
        IEnumerable<CustomFieldDefinition> definiciones,
        IReadOnlyDictionary<Guid, string?> valoresGuardados)
    {
        var todas = definiciones.ToList();
        var conFormula = todas.Where(d => TipoDeCampo.SeCalcula(d.Tipo) && d.Formula is not null).ToList();

        if (conFormula.Count == 0)
            return [];

        var comparador = DetectorDeCiclosDeFormula.ComparadorDeNombres;

        // Los campos que se rellenan a mano y sirven como número. Un campo de texto que
        // «parece un número» no entra: haría que la fórmula funcionara según lo que alguien
        // tecleara ese día, y el fallo saldría como un hueco sin explicación.
        var numeros = new Dictionary<string, decimal?>(comparador);
        foreach (var d in todas.Where(d => d.Tipo == TipoDeCampo.Numero))
        {
            var texto = valoresGuardados.TryGetValue(d.Id, out var v) ? v : null;

            numeros[d.Nombre] = decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
                ? n
                : null;   // sin rellenar, o ilegible: hueco
        }

        // Se analizan una vez, no una por cada referencia.
        var arboles = new Dictionary<string, AnalizadorDeFormula.Nodo>(comparador);
        var definicionPorNombre = new Dictionary<string, CustomFieldDefinition>(comparador);
        var errores = new Dictionary<string, string>(comparador);

        foreach (var d in conFormula)
        {
            definicionPorNombre[d.Nombre] = d;

            var analisis = AnalizadorDeFormula.Analizar(d.Formula);

            if (analisis.EsValida)
                arboles[d.Nombre] = analisis.Arbol!;
            else
                // Una fórmula guardada que ya no se puede leer sólo debería ocurrir si alguien
                // tocó la base a mano o si cambió el lenguaje. Se informa por campo en vez de
                // reventar el formulario entero: el resto de campos siguen siendo útiles.
                errores[d.Nombre] = analisis.Error!;
        }

        var dependencias = arboles.Select(par =>
            new DetectorDeCiclosDeFormula.Dependencia(par.Key, AnalizadorDeFormula.ReferenciasDe(par.Value)));

        var orden = DetectorDeCiclosDeFormula.OrdenDeCalculo(dependencias);

        // Un ciclo aquí significa que se guardaron fórmulas que la comprobación de alta debería
        // haber rechazado. No se intenta evaluar ninguna: la alternativa sería colgarse.
        if (orden is null)
        {
            return conFormula
                .Select(d => new Calculado(d.Id, d.Nombre, null, CustomFieldDefinition.Reglas.FormulaEnCiclo))
                .ToList();
        }

        var calculados = new Dictionary<string, decimal?>(comparador);
        var resultados = new List<Calculado>();

        foreach (var nombre in orden)
        {
            if (!arboles.TryGetValue(nombre, out var arbol))
                continue;   // es un campo normal que aparece en el orden por ser dependencia

            var resultado = EvaluadorDeFormula.Evaluar(arbol, referencia =>
            {
                if (calculados.TryGetValue(referencia, out var yaCalculado))
                    return yaCalculado;

                if (numeros.TryGetValue(referencia, out var numero))
                    return numero;

                // Referencia a un campo que existe pero no sirve en una fórmula —un texto, una
                // fecha—, o a uno que no existe. En ambos casos es un error de la fórmula, no un
                // hueco: quien la escribió tiene que arreglarla.
                throw new EvaluadorDeFormula.CampoDesconocidoException(referencia);
            });

            calculados[nombre] = resultado.Valor;

            var definicion = definicionPorNombre[nombre];
            resultados.Add(new Calculado(definicion.Id, nombre, resultado.Texto, resultado.Error));
        }

        // Las que no se pudieron ni analizar no entraron en el orden; salen igualmente, con su
        // error, para que la pantalla pueda decir qué pasa en lugar de no mostrar el campo.
        foreach (var (nombre, error) in errores)
        {
            var definicion = definicionPorNombre[nombre];
            resultados.Add(new Calculado(definicion.Id, nombre, null, error));
        }

        return resultados;
    }
}
