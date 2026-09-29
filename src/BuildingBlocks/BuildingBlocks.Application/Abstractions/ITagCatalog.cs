namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Qué etiquetas existen en una organización, para los módulos que etiquetan cosas.
///
/// **Es un puerto por lo mismo que <see cref="IUserFavorites"/>:** las etiquetas viven en Tags,
/// pero tareas, tickets, proyectos, informes y dashboards guardan sus ids (<c>TagIds</c>), y ningún
/// módulo referencia a otro. Lo implementa Tags.
///
/// Sin esta comprobación un cliente podría colgar de una tarea el id de una etiqueta inventada o de
/// otra organización: se guardaría, y la pantalla la pintaría vacía o, peor, con el nombre de una
/// etiqueta ajena.
/// </summary>
public interface ITagCatalog
{
    /// <summary>Los ids de la lista que no son etiquetas de la organización. Vacía si todas lo son.</summary>
    Task<IReadOnlyList<Guid>> FindUnknownAsync(Guid tenantId, IReadOnlyCollection<Guid> tagIds, CancellationToken ct = default);
}
