namespace BuildingBlocks.Application;

/// <summary>
/// Todo lo que una consulta de listado necesita para aplicar la entrada de menú que se ha
/// pulsado: qué filtro es, quién pregunta, y las listas de identificadores que sólo se pueden
/// resolver fuera del módulo.
///
/// <b>Es un objeto y no cinco parámetros sueltos por experiencia propia.</b> La consulta de
/// tickets ya iba por diez argumentos, tres de ellos opcionales y del mismo tipo, y al añadir
/// los últimos los dobles de prueba dejaron de compilar con un error de argumentos ambiguos que
/// no decía nada del problema real. Cada concepto nuevo del menú añadía un parámetro más a
/// cuatro módulos. Así se añade un campo aquí y ya está.
///
/// Las listas nunca son nulas. Una lista vacía significa «no hay nada de eso», y filtrar por
/// ella da cero resultados: es justo lo que debe pasar, y un nulo invitaría a interpretarlo como
/// «no filtres», que devolvería la lista entera y volvería a ser un menú que promete y no
/// cumple.
/// </summary>
public sealed record ViewScope(
    string? Filter = null,
    Guid? UserId = null,
    IReadOnlyList<Guid>? FavoriteIds = null,
    IReadOnlyList<Guid>? SharedWithMeIds = null,
    IReadOnlyList<Guid>? SharedWithOthersIds = null)
{
    /// <summary>Lo que marcó esta persona con la estrella.</summary>
    public IReadOnlyList<Guid> Favorites { get; } = FavoriteIds ?? [];

    /// <summary>Lo que alguien compartió con esta persona en concreto.</summary>
    public IReadOnlyList<Guid> SharedWithMe { get; } = SharedWithMeIds ?? [];

    /// <summary>
    /// Lo que está compartido con alguien, sea quien sea. Sirve para lo contrario: «privado» es
    /// lo mío que no está aquí.
    /// </summary>
    public IReadOnlyList<Guid> SharedWithOthers { get; } = SharedWithOthersIds ?? [];

    /// <summary>Sin entrada de menú pulsada: la lista de siempre.</summary>
    public static ViewScope None { get; } = new();

    /// <summary>Si el filtro pide ver la papelera o el archivo, que el filtro global esconde.</summary>
    public bool IncludesHidden => ViewFilters.IncludesHidden(Filter);

    public bool Is(string value) => ViewFilters.Is(Filter, value);
}
