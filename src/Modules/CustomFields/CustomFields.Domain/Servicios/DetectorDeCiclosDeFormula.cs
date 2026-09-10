namespace CustomFields.Domain.Servicios;

/// <summary>
/// Decide si una fórmula se referiría a sí misma, directa o indirectamente.
///
/// Sin esto, `[A] = [B] + 1` con `[B] = [A] + 1` deja la evaluación girando hasta agotar la
/// pila, y eso ocurre **dentro de una petición**: no es una fórmula rara que da un número raro,
/// es la aplicación cayéndose cada vez que alguien abre esa tarea.
///
/// Es una función pura sobre nombres, igual que <c>DetectorDeCiclos</c> en WorkItems y por el
/// mismo motivo: es la regla que más fácil se rompe al refactorizar y la que más caro sale
/// equivocar, y sin base de datos delante se puede probar exhaustivamente con grafos pequeños.
///
/// **Se comprueba al guardar la fórmula, no al evaluarla.** Rechazarla en el formulario, donde
/// quien la escribe puede corregirla, es infinitamente mejor que descubrirlo al abrir una tarea
/// tres semanas después. El evaluador tiene igualmente su tope de profundidad, porque los datos
/// pueden llegar por otras vías.
/// </summary>
public static class DetectorDeCiclosDeFormula
{
    /// <summary>
    /// Los nombres de campo se comparan sin distinguir mayúsculas ni acentos de más: quien
    /// escribe `[horas estimadas]` se refiere a «Horas estimadas». Que la fórmula funcione o no
    /// según cómo se teclee una mayúscula sería una crueldad innecesaria.
    /// </summary>
    public static readonly StringComparer ComparadorDeNombres = StringComparer.OrdinalIgnoreCase;

    /// <summary>Qué campos referencia cada campo con fórmula.</summary>
    public readonly record struct Dependencia(string Campo, IReadOnlyList<string> Referencias);

    /// <summary>
    /// Si dar a <paramref name="campo"/> esas <paramref name="referencias"/> cerraría un ciclo,
    /// contando las fórmulas que ya existen.
    ///
    /// <paramref name="existentes"/> no debe incluir al propio campo: se sustituye por lo que se
    /// está intentando guardar. Si se incluyera, editar una fórmula se compararía contra su
    /// versión anterior y daría ciclos donde no los hay.
    /// </summary>
    public static bool CerrariaUnCiclo(
        IEnumerable<Dependencia> existentes,
        string campo,
        IEnumerable<string> referencias)
    {
        var referenciasDe = new Dictionary<string, IReadOnlyList<string>>(ComparadorDeNombres);

        foreach (var d in existentes)
            referenciasDe[d.Campo] = d.Referencias;

        var nuevas = referencias.ToList();
        referenciasDe[campo] = nuevas;

        // Referirse a uno mismo es el ciclo más corto y el más fácil de escribir sin querer.
        if (nuevas.Contains(campo, ComparadorDeNombres))
            return true;

        // Recorrido iterativo con conjunto de visitados. Recursivo sería más corto, pero un
        // grafo que ya tuviera un ciclo —por datos anteriores a esta comprobación, o por dos
        // escrituras a la vez— haría girar para siempre a la versión ingenua, que es justo el
        // fallo que se quiere evitar.
        var porVisitar = new Stack<string>(nuevas);
        var vistos = new HashSet<string>(ComparadorDeNombres);

        while (porVisitar.Count > 0)
        {
            var actual = porVisitar.Pop();

            if (ComparadorDeNombres.Equals(actual, campo))
                return true;

            if (!vistos.Add(actual))
                continue;

            if (referenciasDe.TryGetValue(actual, out var siguientes))
                foreach (var s in siguientes)
                    porVisitar.Push(s);
        }

        return false;
    }

    /// <summary>
    /// El orden en que hay que calcular los campos para que ninguno dependa de otro que aún no
    /// se ha calculado. <c>null</c> si hay un ciclo.
    ///
    /// Hace falta cuando se calculan varios campos de golpe —al pintar el formulario de una
    /// tarea, por ejemplo—: sin orden, un campo que depende de otro con fórmula se evaluaría
    /// contra un valor que todavía no existe y saldría como hueco.
    /// </summary>
    public static IReadOnlyList<string>? OrdenDeCalculo(IEnumerable<Dependencia> dependencias)
    {
        var referenciasDe = dependencias.ToDictionary(d => d.Campo, d => d.Referencias, ComparadorDeNombres);
        var orden = new List<string>();
        var estado = new Dictionary<string, int>(ComparadorDeNombres); // 1 = en curso, 2 = terminado

        foreach (var campo in referenciasDe.Keys)
            if (!Visitar(campo))
                return null;

        return orden;

        bool Visitar(string campo)
        {
            if (estado.TryGetValue(campo, out var e))
                return e != 1;   // volver a entrar en uno «en curso» es un ciclo

            // Un campo sin fórmula es una hoja: su valor lo pone quien rellena el formulario.
            if (!referenciasDe.TryGetValue(campo, out var referencias))
                return true;

            estado[campo] = 1;

            foreach (var referencia in referencias)
                if (!Visitar(referencia))
                    return false;

            estado[campo] = 2;
            orden.Add(campo);
            return true;
        }
    }
}
