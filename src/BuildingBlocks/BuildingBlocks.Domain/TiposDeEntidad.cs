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
/// </summary>
public static class TiposDeEntidad
{
    public const string Tarea = "Tarea";
    public const string Proyecto = "Proyecto";
    public const string Ticket = "Ticket";
    public const string Documento = "Documento";

    public static IReadOnlyList<string> Todos() => [Tarea, Proyecto, Ticket, Documento];

    public static bool Existe(string? tipo) => tipo is not null && Todos().Contains(tipo);
}
