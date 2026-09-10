namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Qué le han compartido explícitamente a quien está haciendo la petición.
///
/// **Es un puerto, por la misma razón que <see cref="IFavoritosDelUsuario"/>.** Los permisos por
/// entidad viven en Identity (<c>EntityPermission</c>), pero «compartido conmigo» y «privado»
/// son entradas del menú de Tareas, Tickets, Proyectos y Documentos, y ningún módulo referencia
/// a otro. El contrato se declara aquí y lo implementa quien sabe responderlo.
///
/// Se reutiliza la tabla que ya existe en vez de crear una de compartición nueva: dos tablas
/// diciendo quién ve qué acabarían discrepando, y entonces la pregunta «¿quién ve esto?» tendría
/// dos respuestas y ninguna fiable.
/// </summary>
public interface IVisibilidadDeEntidades
{
    /// <summary>
    /// Los identificadores de ese tipo que alguien me ha compartido a mí en concreto.
    ///
    /// Sólo lo compartido **de forma explícita y nominal**: no entra lo que veo por pertenecer a
    /// un equipo ni por mi rol. Es deliberado. Un administrador ve casi todo por su rol, así que
    /// incluir esos permisos convertiría «compartido conmigo» en «todo» y tendríamos otra
    /// entrada de menú que promete y no filtra.
    ///
    /// Lista vacía si no hay nada compartido, y hay que tratarla como tal: cero resultados, no
    /// todos.
    /// </summary>
    Task<IReadOnlyList<Guid>> CompartidosConmigoAsync(string tipoDeEntidad, CancellationToken ct = default);

    /// <summary>
    /// Los identificadores de ese tipo que están compartidos **con alguien**, sea quien sea.
    ///
    /// Sirve para lo contrario: «privado» es lo mío que no aparece en esta lista. Se resuelve
    /// así, y no con un campo <c>EsPrivado</c> en cada agregado, porque un campo sería una
    /// segunda fuente de verdad que se desincroniza en cuanto alguien comparte por otra vía y
    /// entonces el menú enseña como privado algo que ya no lo es.
    /// </summary>
    Task<IReadOnlyList<Guid>> CompartidosConAlguienAsync(string tipoDeEntidad, CancellationToken ct = default);
}
