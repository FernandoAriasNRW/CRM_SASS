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
public static class ViewFilters
{
    /// <summary>
    /// Lo que me toca a mí. Cada módulo decide qué significa: responsable de la tarea, dueño del
    /// proyecto, agente asignado al ticket.
    /// </summary>
    public const string Mine = "mine";

    /// <summary>Lo que yo creé, aunque ahora responda otro.</summary>
    public const string CreatedByMe = "created";

    /// <summary>Lo que marqué con la estrella.</summary>
    public const string Favorites = "favorites";

    /// <summary>De mi equipo. Ya existía en tareas y proyectos.</summary>
    public const string MyTeam = "team";

    /// <summary>
    /// Lo que otra persona me compartió explícitamente. No incluye lo que veo por ser de mi
    /// equipo o por mi rol: eso ya se ve en «ver todo», y mezclarlo dejaría esta entrada
    /// devolviendo prácticamente la lista entera.
    /// </summary>
    public const string SharedWithMe = "shared";

    /// <summary>Mío y de nadie más: lo que llevo yo y no he compartido con nadie.</summary>
    public const string Private = "private";

    /// <summary>
    /// Apartado de la vista, sin borrar. Es el único filtro que **añade** filas en vez de
    /// quitarlas: sin él lo archivado no sale por ninguna parte.
    /// </summary>
    public const string Archived = "archived";

    /// <summary>Borrado y recuperable. Igual que <see cref="Archived"/>, abre el filtro global.</summary>
    public const string Trash = "trash";

    public static IReadOnlyList<string> All() =>
        [Mine, CreatedByMe, Favorites, MyTeam, SharedWithMe, Private, Archived, Trash];

    /// <summary>
    /// Si el filtro pide ver cosas que el filtro global esconde —la papelera o el archivo—.
    ///
    /// Se pregunta antes de lanzar la consulta, porque abrir ese alcance es una decisión
    /// consciente y no algo que un `Where` pueda hacer por su cuenta: lo que el filtro global
    /// esconde no está en el conjunto que la consulta puede filtrar.
    /// </summary>
    public static bool IncludesHidden(string? filter) =>
        Is(filter, Archived) || Is(filter, Trash);

    public static bool Exists(string? filter) =>
        !string.IsNullOrWhiteSpace(filter) && All().Contains(filter, StringComparer.OrdinalIgnoreCase);

    public static bool Is(string? filter, string value) =>
        string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);
}
