namespace BuildingBlocks.Domain;

/// <summary>
/// Cómo se llama cada cosa cuando un módulo tiene que hablar de ella con otro.
///
/// <b>Está aquí porque la cadena se escribía a mano.</b> Los favoritos viven en Identity y el
/// filtro se aplica en Ticketing, que no puede referenciarlo: Ticketing escribía <c>"Ticket"</c>
/// literal y confiaba en que coincidiera. Si algún día no coincidiera, el filtro devolvería una
/// lista vacía **sin dar ningún error** —la clase de fallo que nadie detecta— y hubo que
/// escribir una prueba de integración sólo para vigilar esa cadena.
///
/// Con dos consumidores (favoritos y visibilidad) y cuatro módulos, la cadena estaría en ocho
/// sitios. Aquí está en uno, y la prueba sigue vigilando la unión de los dos lados.
///
/// Va en BuildingBlocks.Domain y no en Application porque el agregado de favoritos la valida:
/// un tipo desconocido se rechaza en el dominio, no en el borde.
///
/// <b>Los valores se guardan</b> en favoritos, comentarios, menciones y campos personalizados, y
/// viajan dentro del HTML de las páginas. Cambiar uno exige migrar esas tablas en el mismo cambio,
/// como se hizo al pasarlos de «Tarea» a «Task» (migraciones <c>StoredValuesToEnglish</c>).
/// </summary>
public static class EntityTypes
{
    public const string Task = "Task";
    public const string Project = "Project";
    public const string Ticket = "Ticket";
    public const string Document = "Document";

    public static IReadOnlyList<string> All() => [Task, Project, Ticket, Document];

    public static bool Exists(string? type) => type is not null && All().Contains(type);
}
