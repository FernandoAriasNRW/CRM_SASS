using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Identity.Domain.Entities;

/// <summary>Sobre qué se puede poner una estrella.</summary>
public static class TipoDeFavorito
{
    public const string Tarea = "Tarea";
    public const string Proyecto = "Proyecto";
    public const string Ticket = "Ticket";
    public const string Documento = "Documento";

    public static IReadOnlyList<string> Todos() => [Tarea, Proyecto, Ticket, Documento];

    public static bool Existe(string tipo) => Todos().Contains(tipo);
}

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
public sealed class Favorito : AggregateRoot, ITenantEntity
{
    /// <summary>
    /// Cuántas estrellas puede tener una persona por tipo.
    ///
    /// No es una restricción de producto sino de sentido: una lista de favoritos con dos mil
    /// elementos no es una lista de favoritos, y además es lo que convierte el filtro en una
    /// consulta con dos mil identificadores dentro.
    /// </summary>
    public const int MaximoPorPersonaYTipo = 200;

    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Uno de <see cref="TipoDeFavorito"/>.</summary>
    public string Tipo { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }
    public DateTime MarcadoUtc { get; private set; }

    private Favorito() { }

    public static Favorito Marcar(Guid tenantId, Guid userId, string tipo, Guid entityId)
    {
        if (!TipoDeFavorito.Existe(tipo))
            throw new InvalidOperationException(Reglas.TipoDesconocido);

        if (entityId == Guid.Empty)
            throw new InvalidOperationException(Reglas.SinEntidad);

        return new Favorito
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Tipo = tipo,
            EntityId = entityId,
            MarcadoUtc = DateTime.UtcNow,
        };
    }

    public static class Reglas
    {
        public const string TipoDesconocido = "Ese tipo de elemento no se puede marcar como favorito";
        public const string SinEntidad = "Falta el elemento que se quiere marcar";
        public static readonly string Demasiados =
            $"No se pueden tener más de {MaximoPorPersonaYTipo} favoritos del mismo tipo";
    }
}
