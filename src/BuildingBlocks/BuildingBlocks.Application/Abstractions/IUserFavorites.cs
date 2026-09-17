namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Qué ha marcado como favorito quien está haciendo la petición.
///
/// **Es un puerto y está aquí a propósito.** Los favoritos viven en Identity —una estrella es
/// algo que una persona decidió, no un atributo de la tarea— pero el filtro «favoritos» tiene
/// que aplicarse dentro de la consulta de cada módulo, y ningún módulo referencia a otro.
///
/// La solución es la misma que ya se usa con <see cref="IUserContext"/>: el contrato se declara
/// aquí, donde todos miran, y quien sabe responderlo lo implementa. Así Ticketing puede
/// preguntar «¿qué tickets ha marcado esta persona?» sin conocer a Identity, y el día que los
/// favoritos cambien de sitio no hay que tocar cuatro módulos.
///
/// La alternativa era que cada módulo tuviera su propia tabla de favoritos: cuatro sitios donde
/// arreglar el mismo fallo y cuatro formas distintas de contar lo mismo.
/// </summary>
public interface IUserFavorites
{
    /// <summary>
    /// Los identificadores marcados de un tipo, de lo más reciente a lo más antiguo.
    ///
    /// Devuelve una lista vacía si no hay ninguno, y quien la use debe tratarla como tal: filtrar
    /// por una lista vacía da cero resultados, no todos. Devolver todo cuando no hay favoritos
    /// sería el mismo engaño que un menú que promete y no filtra.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetIdsAsync(string entityType, CancellationToken ct = default);
}
