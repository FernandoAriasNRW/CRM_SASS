namespace BuildingBlocks.Application;

/// <summary>
/// Los filtros que el panel de navegación ofrece en todos los módulos.
///
/// **Están aquí y no repetidos en cada módulo para que no se desincronicen.** Es exactamente el
/// problema que ya se dio dos veces en este producto: el menú ofrecía «Mis Tickets» con
/// `?filter=mine` y el backend de Ticketing no tenía ese filtro, así que devolvía los 175
/// tickets de siempre; y el desplegable de informes ofrecía dos tipos que el enum del servidor
/// no conocía. En los dos casos la pantalla prometía y la respuesta era la de siempre.
///
/// Un menú que promete y devuelve la misma lista es peor que un menú corto: quien lo usa cree
/// que está viendo lo suyo y está viendo todo.
///
/// Cada módulo interpreta «mío» a su manera —quien responde de una tarea, quien es dueño de un
/// proyecto, el agente de un ticket— y eso es correcto: el nombre es común, el significado es
/// del dominio de cada uno. Lo que no puede variar es el nombre.
/// </summary>
public static class FiltrosDeVista
{
    /// <summary>
    /// Lo que me toca a mí. Cada módulo decide qué significa: responsable de la tarea, dueño del
    /// proyecto, agente asignado al ticket.
    /// </summary>
    public const string Mios = "mine";

    /// <summary>Lo que yo creé, aunque ahora responda otro.</summary>
    public const string CreadosPorMi = "created";

    /// <summary>Lo que marqué con la estrella.</summary>
    public const string Favoritos = "favorites";

    /// <summary>De mi equipo. Ya existía en tareas y proyectos.</summary>
    public const string DeMiEquipo = "team";

    public static IReadOnlyList<string> Todos() => [Mios, CreadosPorMi, Favoritos, DeMiEquipo];

    public static bool Existe(string? filtro) =>
        !string.IsNullOrWhiteSpace(filtro) && Todos().Contains(filtro, StringComparer.OrdinalIgnoreCase);

    public static bool Es(string? filtro, string cual) =>
        string.Equals(filtro, cual, StringComparison.OrdinalIgnoreCase);
}
