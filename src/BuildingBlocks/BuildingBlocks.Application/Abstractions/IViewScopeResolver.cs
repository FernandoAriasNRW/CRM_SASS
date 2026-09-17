namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Prepara el <see cref="ViewScope"/> de una petición resolviendo las listas que el módulo
/// no puede resolver por su cuenta.
///
/// Está aquí para que cada endpoint no tenga que acordarse de pedir los favoritos y lo
/// compartido, ni de saber para qué filtros hace falta cada cosa. Ese «acordarse» es
/// exactamente lo que falló antes: el endpoint de tickets recibía <c>filter=mine</c> y no hacía
/// nada con él.
/// </summary>
public interface IViewScopeResolver
{
    /// <summary>
    /// Resuelve el alcance para un tipo de entidad —«Tarea», «Ticket», «Proyecto», «Documento»—.
    ///
    /// Sólo consulta lo que el filtro pedido necesita: pedir favoritos y compartidos en cada
    /// listado serían dos consultas de más en la pantalla que más se abre.
    /// </summary>
    Task<ViewScope> ResolveAsync(
        string? filter, Guid? userId, string entityType, CancellationToken ct = default);
}
