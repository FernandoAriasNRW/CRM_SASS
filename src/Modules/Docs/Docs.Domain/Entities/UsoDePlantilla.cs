using BuildingBlocks.Domain.Primitives;

namespace Docs.Domain.Entities;

/// <summary>
/// Cuántas veces se ha creado un documento a partir de una plantilla, por inquilino.
///
/// La galería enseña sólo cuatro plantillas y hay muchas más detrás del «ver más», así que
/// alguien tiene que decidir cuáles son esas cuatro. Ordenarlas por uso es lo único que no
/// obliga a inventar un criterio: las que el equipo abre a diario suben solas.
///
/// <b>Se cuenta aquí y no en el documento</b> porque las plantillas predefinidas no son filas:
/// viven en el código, identificadas por clave. Una tabla con una <see cref="Clave"/> de texto
/// admite las dos —la clave del sistema, o el identificador de la plantilla propia— y evita
/// tener dos contadores que se cuentan distinto.
///
/// El contador es por inquilino, no por persona. Lo que se ofrece es «lo que aquí se usa», no
/// «lo que tú usas»: alguien que entra nuevo se encuentra la galería del equipo ya ordenada en
/// vez de cuatro plantillas al azar.
/// </summary>
public sealed class UsoDePlantilla : Entity, ITenantEntity
{
    public Guid TenantId { get; private set; }

    /// <summary>
    /// Qué plantilla. O la clave de una predefinida —<c>meeting-notes</c>— o el identificador
    /// de un documento de tipo plantilla, en texto.
    /// </summary>
    public string Clave { get; private set; } = string.Empty;

    public int Veces { get; private set; }

    public DateTime UltimoUsoUtc { get; private set; }

    private UsoDePlantilla() { }

    public static UsoDePlantilla Primera(Guid tenantId, string clave)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Clave = clave,
            Veces = 1,
            UltimoUsoUtc = DateTime.UtcNow
        };

    public void Sumar()
    {
        Veces++;
        UltimoUsoUtc = DateTime.UtcNow;
    }
}
