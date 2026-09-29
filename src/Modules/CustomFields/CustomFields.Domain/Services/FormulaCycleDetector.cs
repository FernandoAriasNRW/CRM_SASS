namespace CustomFields.Domain.Services;

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
public static class FormulaCycleDetector
{
    /// <summary>
    /// Los nombres de campo se comparan sin distinguir mayúsculas ni acentos de más: quien
    /// escribe `[horas estimadas]` se refiere a «Horas estimadas». Que la fórmula funcione o no
    /// según cómo se teclee una mayúscula sería una crueldad innecesaria.
    /// </summary>
    public static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>Qué campos referencia cada campo con fórmula.</summary>
    public readonly record struct Dependency(string Field, IReadOnlyList<string> References);

    /// <summary>
    /// Si dar a <paramref name="field"/> esas <paramref name="references"/> cerraría un ciclo,
    /// contando las fórmulas que ya existen.
    ///
    /// <paramref name="existingFields"/> no debe incluir al propio campo: se sustituye por lo que se
    /// está intentando guardar. Si se incluyera, editar una fórmula se compararía contra su
    /// versión anterior y daría ciclos donde no los hay.
    /// </summary>
    public static bool WouldCreateCycle(
        IEnumerable<Dependency> existingFields,
        string field,
        IEnumerable<string> references)
    {
        var referencesOf = new Dictionary<string, IReadOnlyList<string>>(NameComparer);

        foreach (var d in existingFields)
            referencesOf[d.Field] = d.References;

        var added = references.ToList();
        referencesOf[field] = added;

        // Referirse a uno mismo es el ciclo más corto y el más fácil de escribir sin querer.
        if (added.Contains(field, NameComparer))
            return true;

        // Recorrido iterativo con conjunto de visitados. Recursivo sería más corto, pero un
        // grafo que ya tuviera un ciclo —por datos anteriores a esta comprobación, o por dos
        // escrituras a la vez— haría girar para siempre a la versión ingenua, que es justo el
        // fallo que se quiere evitar.
        var toVisit = new Stack<string>(added);
        var seen = new HashSet<string>(NameComparer);

        while (toVisit.Count > 0)
        {
            var actual = toVisit.Pop();

            if (NameComparer.Equals(actual, field))
                return true;

            if (!seen.Add(actual))
                continue;

            if (referencesOf.TryGetValue(actual, out var next))
                foreach (var s in next)
                    toVisit.Push(s);
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
    public static IReadOnlyList<string>? ComputationOrder(IEnumerable<Dependency> dependencies)
    {
        var referencesOf = dependencies.ToDictionary(d => d.Field, d => d.References, NameComparer);
        var order = new List<string>();
        var state = new Dictionary<string, int>(NameComparer); // 1 = en curso, 2 = terminado

        foreach (var field in referencesOf.Keys)
            if (!Visit(field))
                return null;

        return order;

        bool Visit(string field)
        {
            if (state.TryGetValue(field, out var e))
                return e != 1;   // volver a entrar en uno «en curso» es un ciclo

            // Un campo sin fórmula es una hoja: su valor lo pone quien rellena el formulario.
            if (!referencesOf.TryGetValue(field, out var references))
                return true;

            state[field] = 1;

            foreach (var reference in references)
                if (!Visit(reference))
                    return false;

            state[field] = 2;
            order.Add(field);
            return true;
        }
    }
}
