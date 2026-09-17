namespace WorkItems.Domain.Services;

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
public static class CycleDetector
{
    public readonly record struct Edge(Guid Task, Guid DependsOn);

    /// <summary>
    /// Si añadir <paramref name="task"/> → <paramref name="dependsOn"/> cerraría un ciclo.
    ///
    /// El recorrido es iterativo y con conjunto de visitados: un grafo que ya tuviera un ciclo
    /// —por datos antiguos o por una escritura concurrente— haría girar para siempre a una
    /// versión recursiva ingenua, y esto se ejecuta dentro de una petición.
    /// </summary>
    public static bool WouldCloseCycle(IEnumerable<Edge> edges, Guid task, Guid dependsOn)
    {
        if (task == dependsOn)
            return true;

        // Índice por tarea: a quién espera cada una.
        var waitsFor = new Dictionary<Guid, List<Guid>>();
        foreach (var edge in edges)
        {
            if (!waitsFor.TryGetValue(edge.Task, out var list))
                waitsFor[edge.Task] = list = [];

            list.Add(edge.DependsOn);
        }

        var visited = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(dependsOn);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (current == task)
                return true;

            if (!visited.Add(current))
                continue;

            if (waitsFor.TryGetValue(current, out var nextOnes))
                foreach (var next in nextOnes)
                    pending.Push(next);
        }

        return false;
    }
}
