using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Identity.Domain.Entities;

/// <summary>
/// Una estrella: esta persona marcó esta cosa.
///
/// **Vive en Identity y no en cada módulo a propósito.** Un favorito no es un atributo de la
/// tarea —no es algo que la tarea sea—, es algo que *una persona* decidió sobre ella. Guardarlo
/// en la tarea obligaría a una columna por usuario, que no existe, o a una tabla de relación
/// dentro de WorkItems que Ticketing tendría que duplicar, y luego Docs, y luego el siguiente.
/// Aquí hay una sola tabla y una sola forma de preguntarlo.
///
/// El precio, y hay que decirlo: **filtrar una lista por favoritos son dos consultas**, una para
/// saber qué identificadores marcó la persona y otra para traer esos elementos. Con un límite
/// razonable de estrellas por persona eso es una lista corta de identificadores, y es preferible
/// a que cada módulo se invente su propio mecanismo.
///
/// La alternativa —una tabla de favoritos por módulo— daría cuatro sitios donde arreglar el
/// mismo fallo y cuatro formas distintas de contar lo mismo.
/// </summary>
public sealed class Favorite : AggregateRoot, ITenantEntity
{
    /// <summary>
    /// Cuántas estrellas puede tener una persona por tipo.
    ///
    /// No es una restricción de producto sino de sentido: una lista de favoritos con dos mil
    /// elementos no es una lista de favoritos, y además es lo que convierte el filtro en una
    /// consulta con dos mil identificadores dentro.
    /// </summary>
    public const int MaxPerUserAndType = 200;

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Uno de <see cref="TipoDeFavorito"/>.</summary>
    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }
    public DateTime MarkedAtUtc { get; private set; }

    private Favorite() { }

    public static Favorite Mark(Guid tenantId, Guid userId, string entityType, Guid entityId)
    {
        if (!EntityTypes.Exists(entityType))
            throw new InvalidOperationException(Rules.UnknownType);

        if (entityId == Guid.Empty)
            throw new InvalidOperationException(Rules.MissingEntity);

        return new Favorite
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            MarkedAtUtc = DateTime.UtcNow,
        };
    }

    public static class Rules
    {
        public const string UnknownType = "Ese tipo de elemento no se puede marcar como favorito";
        public const string MissingEntity = "Falta el elemento que se quiere marcar";
        public static readonly string TooMany =
            $"No se pueden tener más de {MaxPerUserAndType} favoritos del mismo tipo";
    }
}
