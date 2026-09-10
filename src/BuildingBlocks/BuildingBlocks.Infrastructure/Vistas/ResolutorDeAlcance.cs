using BuildingBlocks.Application;
using BuildingBlocks.Application.Abstractions;

namespace BuildingBlocks.Infrastructure.Vistas;

/// <summary>
/// Prepara el <see cref="AlcanceDeVista"/> de cada listado.
///
/// Un solo sitio donde se decide qué hace falta consultar para cada entrada del menú. Antes eso
/// vivía en el endpoint —el de tickets pedía los favoritos a mano— y el resultado fue que el
/// endpoint de tareas recibía el mismo parámetro y no pedía nada, así que «Favoritos» habría
/// devuelto la lista entera en un módulo y la correcta en el otro.
/// </summary>
public sealed class ResolutorDeAlcance(
    IFavoritosDelUsuario favoritos,
    IVisibilidadDeEntidades visibilidad) : IAlcanceDeVista
{
    public async Task<AlcanceDeVista> ResolverAsync(
        string? filtro, Guid? usuarioId, string tipoDeEntidad, CancellationToken ct = default)
    {
        // Sin filtro no se consulta nada. Pedir favoritos y compartidos en cada listado serían
        // dos viajes de más a la base en la pantalla que más se abre.
        if (!FiltrosDeVista.Existe(filtro))
            return AlcanceDeVista.Ninguno;

        var idsFavoritos = FiltrosDeVista.Es(filtro, FiltrosDeVista.Favoritos)
            ? await favoritos.IdsAsync(tipoDeEntidad, ct)
            : [];

        var conmigo = FiltrosDeVista.Es(filtro, FiltrosDeVista.CompartidosConmigo)
            ? await visibilidad.CompartidosConmigoAsync(tipoDeEntidad, ct)
            : [];

        // «Privado» es lo mío que no está compartido con nadie, así que necesita la lista de lo
        // compartido para restarla. Es la única entrada que consulta para excluir.
        var conAlguien = FiltrosDeVista.Es(filtro, FiltrosDeVista.Privados)
            ? await visibilidad.CompartidosConAlguienAsync(tipoDeEntidad, ct)
            : [];

        return new AlcanceDeVista(filtro, usuarioId, idsFavoritos, conmigo, conAlguien);
    }
}
