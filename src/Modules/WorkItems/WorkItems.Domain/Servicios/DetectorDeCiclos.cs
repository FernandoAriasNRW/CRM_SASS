namespace WorkItems.Domain.Servicios;

/// <summary>
/// Decide si añadir una dependencia cerraría un ciclo.
///
/// Es una función pura sobre las aristas a propósito: la detección de ciclos es la regla que
/// más fácil se rompe al refactorizar y la que más caro sale equivocar —un ciclo hace que el
/// Gantt de la 4C no tenga solución y que cualquier cálculo de «qué puedo empezar» se cuelgue o
/// mienta—. Sin base de datos delante se puede probar exhaustivamente con grafos pequeños.
///
/// La arista se lee «<c>Tarea</c> está bloqueada por <c>DependeDe</c>». Añadir
/// <c>a → b</c> cierra un ciclo si desde <c>b</c> ya se llega a <c>a</c> siguiendo esa misma
/// dirección.
/// </summary>
public static class DetectorDeCiclos
{
    public readonly record struct Arista(Guid Tarea, Guid DependeDe);

    /// <summary>
    /// Si añadir <paramref name="tarea"/> → <paramref name="dependeDe"/> cerraría un ciclo.
    ///
    /// El recorrido es iterativo y con conjunto de visitados: un grafo que ya tuviera un ciclo
    /// —por datos antiguos o por una escritura concurrente— haría girar para siempre a una
    /// versión recursiva ingenua, y esto se ejecuta dentro de una petición.
    /// </summary>
    public static bool CerrariaUnCiclo(IEnumerable<Arista> aristas, Guid tarea, Guid dependeDe)
    {
        if (tarea == dependeDe)
            return true;

        // Índice por tarea: a quién espera cada una.
        var esperaA = new Dictionary<Guid, List<Guid>>();
        foreach (var arista in aristas)
        {
            if (!esperaA.TryGetValue(arista.Tarea, out var lista))
                esperaA[arista.Tarea] = lista = [];

            lista.Add(arista.DependeDe);
        }

        var visitados = new HashSet<Guid>();
        var pendientes = new Stack<Guid>();
        pendientes.Push(dependeDe);

        while (pendientes.Count > 0)
        {
            var actual = pendientes.Pop();

            if (actual == tarea)
                return true;

            if (!visitados.Add(actual))
                continue;

            if (esperaA.TryGetValue(actual, out var siguientes))
                foreach (var siguiente in siguientes)
                    pendientes.Push(siguiente);
        }

        return false;
    }
}
