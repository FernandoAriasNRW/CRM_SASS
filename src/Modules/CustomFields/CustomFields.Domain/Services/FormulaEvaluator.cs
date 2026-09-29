using System.Globalization;
using static CustomFields.Domain.Services.FormulaParser;

namespace CustomFields.Domain.Services;

/// <summary>
/// Calcula el valor de una fórmula ya analizada.
///
/// Función pura: recibe el árbol y una forma de preguntar «cuánto vale el campo X». No sabe de
/// base de datos ni de peticiones, así que se puede probar con casos que en producción tardarían
/// meses en darse.
///
/// ── Un campo sin rellenar hace que el resultado no exista ──────────────────────────────────
///
/// Es la decisión que más se nota al usarlo, y va a contracorriente de lo que hace una hoja de
/// cálculo, donde una celda vacía vale cero.
///
/// El motivo: un total que dice «1.200 €» cuando la mitad de sus sumandos están en blanco es
/// peor que uno que no dice nada. El primero se lee como un dato y se usa para decidir; el
/// segundo se ve que falta rellenarlo. Tratar el hueco como cero convierte «no lo sé» en «es
/// cero», que son cosas distintas y sólo una de las dos es verdad.
///
/// Es el mismo criterio que en el panel de informes, donde el tiempo de ciclo viaja como nulo
/// en vez de como 0 porque no hay con qué calcularlo.
///
/// La excepción es <c>SI</c>: si la condición se puede evaluar, la rama que no se toma da igual
/// que esté incompleta. Eso es lo que permite escribir `SI([Horas] > 0; [Coste] / [Horas]; 0)`
/// y que funcione cuando las horas están en blanco.
/// </summary>
public static class FormulaEvaluator
{
    /// <summary>
    /// Cuántos decimales se conservan. Doce es de sobra para dinero y porcentajes, y evita que
    /// una división periódica arrastre una cola infinita hasta la pantalla.
    /// </summary>
    public const int MaxDecimals = 12;

    /// <summary>
    /// El resultado de evaluar.
    ///
    /// Tres estados y no dos: <c>Valor</c> cuando hay número, <c>SinDato</c> cuando faltaba
    /// algún campo, y <c>Error</c> cuando la fórmula no se puede calcular —dividir por cero, un
    /// campo que ya no existe—. Juntar los dos últimos escondería un error real detrás de un
    /// hueco que parece normal.
    /// </summary>
    public sealed record EvaluationResult(decimal? Value, bool NoData, string? Error)
    {
        public bool IsError => Error is not null;
        public bool HasValue => Value.HasValue;

        public static EvaluationResult Of(decimal value) => new(value, false, null);
        public static readonly EvaluationResult Blank = new(null, true, null);
        public static EvaluationResult Fail(string error) => new(null, false, error);

        /// <summary>Cómo se guarda y se enseña: con punto decimal, sin ceros de relleno.</summary>
        public string? Text => Value?.ToString("0.############", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Evalúa el árbol.
    ///
    /// <paramref name="valueOf"/> devuelve el número de un campo por su nombre, o <c>null</c> si
    /// está sin rellenar. Si el campo **no existe**, debe lanzar
    /// <see cref="UnknownFieldException"/>: es un error de la fórmula, no un hueco.
    /// </summary>
    public static EvaluationResult Evaluate(Node tree, Func<string, decimal?> valueOf)
    {
        try
        {
            var value = Compute(tree, valueOf);
            return value is null ? EvaluationResult.Blank : EvaluationResult.Of(Round(value.Value));
        }
        catch (UnknownFieldException ex)
        {
            return EvaluationResult.Fail(string.Format(Errors.UnknownField, ex.Name));
        }
        catch (DivisionByZeroException)
        {
            return EvaluationResult.Fail(Errors.DivisionByZero);
        }
        catch (OverflowException)
        {
            return EvaluationResult.Fail(Errors.TooLarge);
        }
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, MaxDecimals, MidpointRounding.AwayFromZero);

    /// <summary><c>null</c> significa «faltaba algún campo», y se propaga hacia arriba.</summary>
    private static decimal? Compute(Node node, Func<string, decimal?> valueOf) => node switch
    {
        Constant c => c.Value,
        Reference r => valueOf(r.Name),
        Negation n => Compute(n.Operand, valueOf) is { } v ? -v : null,
        Operation o => ComputeOperation(o, valueOf),
        Call l => ComputeCall(l, valueOf),
        _ => throw new InvalidOperationException("Nodo de fórmula no contemplado: " + node.GetType().Name),
    };

    private static decimal? ComputeOperation(Operation o, Func<string, decimal?> valueOf)
    {
        var left = Compute(o.Left, valueOf);
        if (left is null) return null;

        var right = Compute(o.Right, valueOf);
        if (right is null) return null;

        var a = left.Value;
        var b = right.Value;

        return o.Operator switch
        {
            "+" => a + b,
            "-" => a - b,
            "*" => a * b,

            // Dividir por cero es un error y no un hueco: el hueco dice «falta un dato» y aquí
            // los datos están, lo que no se puede es hacer la cuenta. Quien lo vea tiene que
            // arreglar la fórmula, no rellenar un campo.
            "/" => b == 0 ? throw new DivisionByZeroException() : a / b,

            // Las comparaciones dan 1 o 0, que es lo que espera SI y lo que permite sumarlas
            // para contar cuántas condiciones se cumplen.
            "=" => a == b ? 1m : 0m,
            "<>" => a != b ? 1m : 0m,
            "<" => a < b ? 1m : 0m,
            "<=" => a <= b ? 1m : 0m,
            ">" => a > b ? 1m : 0m,
            ">=" => a >= b ? 1m : 0m,

            _ => throw new InvalidOperationException("Operador no contemplado: " + o.Operator),
        };
    }

    private static decimal? ComputeCall(Call l, Func<string, decimal?> valueOf)
    {
        // SI se evalúa perezosamente: sólo la rama que se toma. Así
        // `SI([Horas] > 0; [Coste] / [Horas]; 0)` funciona con las horas en blanco, y además no
        // revienta por una división por cero que nunca llega a ocurrir.
        if (l.Function == Functions.If)
        {
            var condition = Compute(l.Arguments[0], valueOf);
            if (condition is null) return null;

            // Cualquier cosa distinta de cero es verdadero, como en el resto del mundo.
            return Compute(condition.Value != 0 ? l.Arguments[1] : l.Arguments[2], valueOf);
        }

        var values = new List<decimal>(l.Arguments.Count);
        foreach (var argument in l.Arguments)
        {
            var v = Compute(argument, valueOf);
            if (v is null) return null;
            values.Add(v.Value);
        }

        return l.Function switch
        {
            Functions.Abs => Math.Abs(values[0]),
            Functions.Min => values.Min(),
            Functions.Max => values.Max(),
            Functions.Round => RoundTo(values[0], values[1]),
            _ => throw new InvalidOperationException("Función no contemplada: " + l.Function),
        };
    }

    private static decimal RoundTo(decimal value, decimal decimals)
    {
        // Se acota en vez de fallar: pedir 40 decimales es un despiste, no una intención, y
        // `Math.Round` lanza por encima de 28. Fallar aquí obligaría a quien escribe la fórmula
        // a aprenderse un límite del tipo `decimal` que no le importa.
        var count = (int)Math.Clamp(Math.Truncate(decimals), 0, 15);
        return Math.Round(value, count, MidpointRounding.AwayFromZero);
    }

    public sealed class UnknownFieldException(string name) : Exception($"El campo «{name}» no existe")
    {
        public string Name { get; } = name;
    }

    private sealed class DivisionByZeroException : Exception;

    public static class Errors
    {
        public const string DivisionByZero = "La fórmula divide entre cero";
        public const string UnknownField = "La fórmula usa el campo «{0}», que no existe";
        public const string TooLarge = "El resultado es demasiado grande";
    }
}
