using System.Globalization;
using static CustomFields.Domain.Servicios.AnalizadorDeFormula;

namespace CustomFields.Domain.Servicios;

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
public static class EvaluadorDeFormula
{
    /// <summary>
    /// Cuántos decimales se conservan. Doce es de sobra para dinero y porcentajes, y evita que
    /// una división periódica arrastre una cola infinita hasta la pantalla.
    /// </summary>
    public const int DecimalesMaximos = 12;

    /// <summary>
    /// El resultado de evaluar.
    ///
    /// Tres estados y no dos: <c>Valor</c> cuando hay número, <c>SinDato</c> cuando faltaba
    /// algún campo, y <c>Error</c> cuando la fórmula no se puede calcular —dividir por cero, un
    /// campo que ya no existe—. Juntar los dos últimos escondería un error real detrás de un
    /// hueco que parece normal.
    /// </summary>
    public sealed record Resultado(decimal? Valor, bool SinDato, string? Error)
    {
        public bool EsError => Error is not null;
        public bool HayValor => Valor.HasValue;

        public static Resultado De(decimal valor) => new(valor, false, null);
        public static readonly Resultado Hueco = new(null, true, null);
        public static Resultado Mal(string error) => new(null, false, error);

        /// <summary>Cómo se guarda y se enseña: con punto decimal, sin ceros de relleno.</summary>
        public string? Texto => Valor?.ToString("0.############", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Evalúa el árbol.
    ///
    /// <paramref name="valorDe"/> devuelve el número de un campo por su nombre, o <c>null</c> si
    /// está sin rellenar. Si el campo **no existe**, debe lanzar
    /// <see cref="CampoDesconocidoException"/>: es un error de la fórmula, no un hueco.
    /// </summary>
    public static Resultado Evaluar(Nodo arbol, Func<string, decimal?> valorDe)
    {
        try
        {
            var valor = Calcular(arbol, valorDe);
            return valor is null ? Resultado.Hueco : Resultado.De(Redondear(valor.Value));
        }
        catch (CampoDesconocidoException ex)
        {
            return Resultado.Mal(string.Format(Errores.CampoDesconocido, ex.Nombre));
        }
        catch (DivisionPorCeroException)
        {
            return Resultado.Mal(Errores.DivisionPorCero);
        }
        catch (OverflowException)
        {
            return Resultado.Mal(Errores.DemasiadoGrande);
        }
    }

    private static decimal Redondear(decimal valor) =>
        Math.Round(valor, DecimalesMaximos, MidpointRounding.AwayFromZero);

    /// <summary><c>null</c> significa «faltaba algún campo», y se propaga hacia arriba.</summary>
    private static decimal? Calcular(Nodo nodo, Func<string, decimal?> valorDe) => nodo switch
    {
        Constante c => c.Valor,
        Referencia r => valorDe(r.Nombre),
        Negacion n => Calcular(n.Interior, valorDe) is { } v ? -v : null,
        Operacion o => CalcularOperacion(o, valorDe),
        Llamada l => CalcularLlamada(l, valorDe),
        _ => throw new InvalidOperationException("Nodo de fórmula no contemplado: " + nodo.GetType().Name),
    };

    private static decimal? CalcularOperacion(Operacion o, Func<string, decimal?> valorDe)
    {
        var izquierda = Calcular(o.Izquierda, valorDe);
        if (izquierda is null) return null;

        var derecha = Calcular(o.Derecha, valorDe);
        if (derecha is null) return null;

        var a = izquierda.Value;
        var b = derecha.Value;

        return o.Operador switch
        {
            "+" => a + b,
            "-" => a - b,
            "*" => a * b,

            // Dividir por cero es un error y no un hueco: el hueco dice «falta un dato» y aquí
            // los datos están, lo que no se puede es hacer la cuenta. Quien lo vea tiene que
            // arreglar la fórmula, no rellenar un campo.
            "/" => b == 0 ? throw new DivisionPorCeroException() : a / b,

            // Las comparaciones dan 1 o 0, que es lo que espera SI y lo que permite sumarlas
            // para contar cuántas condiciones se cumplen.
            "=" => a == b ? 1m : 0m,
            "<>" => a != b ? 1m : 0m,
            "<" => a < b ? 1m : 0m,
            "<=" => a <= b ? 1m : 0m,
            ">" => a > b ? 1m : 0m,
            ">=" => a >= b ? 1m : 0m,

            _ => throw new InvalidOperationException("Operador no contemplado: " + o.Operador),
        };
    }

    private static decimal? CalcularLlamada(Llamada l, Func<string, decimal?> valorDe)
    {
        // SI se evalúa perezosamente: sólo la rama que se toma. Así
        // `SI([Horas] > 0; [Coste] / [Horas]; 0)` funciona con las horas en blanco, y además no
        // revienta por una división por cero que nunca llega a ocurrir.
        if (l.Funcion == Funciones.Si)
        {
            var condicion = Calcular(l.Argumentos[0], valorDe);
            if (condicion is null) return null;

            // Cualquier cosa distinta de cero es verdadero, como en el resto del mundo.
            return Calcular(condicion.Value != 0 ? l.Argumentos[1] : l.Argumentos[2], valorDe);
        }

        var valores = new List<decimal>(l.Argumentos.Count);
        foreach (var argumento in l.Argumentos)
        {
            var v = Calcular(argumento, valorDe);
            if (v is null) return null;
            valores.Add(v.Value);
        }

        return l.Funcion switch
        {
            Funciones.Absoluto => Math.Abs(valores[0]),
            Funciones.Minimo => valores.Min(),
            Funciones.Maximo => valores.Max(),
            Funciones.Redondear => RedondearA(valores[0], valores[1]),
            _ => throw new InvalidOperationException("Función no contemplada: " + l.Funcion),
        };
    }

    private static decimal RedondearA(decimal valor, decimal decimales)
    {
        // Se acota en vez de fallar: pedir 40 decimales es un despiste, no una intención, y
        // `Math.Round` lanza por encima de 28. Fallar aquí obligaría a quien escribe la fórmula
        // a aprenderse un límite del tipo `decimal` que no le importa.
        var cuantos = (int)Math.Clamp(Math.Truncate(decimales), 0, 15);
        return Math.Round(valor, cuantos, MidpointRounding.AwayFromZero);
    }

    public sealed class CampoDesconocidoException(string nombre) : Exception($"El campo «{nombre}» no existe")
    {
        public string Nombre { get; } = nombre;
    }

    private sealed class DivisionPorCeroException : Exception;

    public static class Errores
    {
        public const string DivisionPorCero = "La fórmula divide entre cero";
        public const string CampoDesconocido = "La fórmula usa el campo «{0}», que no existe";
        public const string DemasiadoGrande = "El resultado es demasiado grande";
    }
}
