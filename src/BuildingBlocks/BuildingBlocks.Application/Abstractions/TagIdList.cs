namespace BuildingBlocks.Application.Abstractions;

/// <summary>Lo que hace cada módulo con la lista de etiquetas que le llega antes de guardarla.</summary>
public static class TagIdList
{
    /// <summary>Límite por elemento: más que suficiente para etiquetar, y corta listas absurdas.</summary>
    public const int MaxTags = 30;

    /// <summary>Sin repetidas ni <c>Guid.Empty</c>, en el orden en que llegaron.</summary>
    public static IReadOnlyList<Guid> Normalize(IEnumerable<Guid>? tagIds)
        => (tagIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();

    /// <summary>
    /// El error que hay que devolver, o <c>null</c> si la lista vale: demasiadas, o alguna que no
    /// es una etiqueta de la organización.
    /// </summary>
    public static async Task<string?> ValidateAsync(
        ITagCatalog catalog, Guid tenantId, IReadOnlyList<Guid> tagIds, CancellationToken ct)
    {
        if (tagIds.Count > MaxTags)
            return $"Se admiten hasta {MaxTags} etiquetas";

        if (tagIds.Count == 0)
            return null;

        var unknown = await catalog.FindUnknownAsync(tenantId, tagIds, ct);
        return unknown.Count == 0
            ? null
            : $"No existen estas etiquetas en la organización: {string.Join(", ", unknown)}";
    }
}
