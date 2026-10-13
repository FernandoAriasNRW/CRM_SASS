using BuildingBlocks.Application.Abstractions;

namespace ApiHost.Reporting;

/// <summary>
/// El nombre de cada persona de un informe, en vez de su identificador.
///
/// Los informes agrupaban por responsable, agente o dueño escribiendo el Guid: los nombres viven
/// en Identity y el informe ya cruzaba dos módulos. Se resuelven de una vez con
/// <see cref="IUserDirectory"/>, que funciona también sin petición, como en una exportación en
/// segundo plano.
/// </summary>
internal static class PersonNames
{
    /// <summary>Lo que se escribe para alguien que ya no está en la organización.</summary>
    public const string Former = "(persona que ya no está)";

    /// <summary>
    /// El nombre de cada identificador. Dos personas con el mismo nombre se distinguen con el
    /// principio de su identificador: si no, sus filas se sumarían en una sola y el informe
    /// atribuiría a una el trabajo de las dos.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IUserDirectory users, Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var found = await users.GetAsync(tenantId, ids, ct);
        var repeated = found.Values.GroupBy(u => u.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

        return ids.Distinct().ToDictionary(id => id, id =>
            !found.TryGetValue(id, out var user) ? Former
            : repeated.Contains(user.Name) ? $"{user.Name} ({id.ToString()[..8]})"
            : user.Name);
    }
}
