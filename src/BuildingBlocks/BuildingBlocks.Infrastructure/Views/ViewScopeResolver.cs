using BuildingBlocks.Application;
using BuildingBlocks.Application.Abstractions;

namespace BuildingBlocks.Infrastructure.Views;

/// <summary>
/// Prepara el <see cref="ViewScope"/> de cada listado.
///
/// Un solo sitio donde se decide qué hace falta consultar para cada entrada del menú. Antes eso
/// vivía en el endpoint —el de tickets pedía los favoritos a mano— y el resultado fue que el
/// endpoint de tareas recibía el mismo parámetro y no pedía nada, así que «Favoritos» habría
/// devuelto la lista entera en un módulo y la correcta en el otro.
/// </summary>
public sealed class ViewScopeResolver(
    IUserFavorites favorites,
    IEntityVisibility visibility) : IViewScopeResolver
{
    public async Task<ViewScope> ResolveAsync(
        string? filtro, Guid? userId, string entityType, CancellationToken ct = default)
    {
        // Sin filtro no se consulta nada. Pedir favoritos y compartidos en cada listado serían
        // dos viajes de más a la base en la pantalla que más se abre.
        if (!ViewFilters.Exists(filtro))
            return ViewScope.None;

        var favoriteIds = ViewFilters.Is(filtro, ViewFilters.Favorites)
            ? await favorites.GetIdsAsync(entityType, ct)
            : [];

        var sharedWithMe = ViewFilters.Is(filtro, ViewFilters.SharedWithMe)
            ? await visibility.GetSharedWithMeAsync(entityType, ct)
            : [];

        // «Privado» es lo mío que no está compartido con nadie, así que necesita la lista de lo
        // compartido para restarla. Es la única entrada que consulta para excluir.
        var sharedWithOthers = ViewFilters.Is(filtro, ViewFilters.Private)
            ? await visibility.GetSharedWithOthersAsync(entityType, ct)
            : [];

        return new ViewScope(filtro, userId, favoriteIds, sharedWithMe, sharedWithOthers);
    }
}
