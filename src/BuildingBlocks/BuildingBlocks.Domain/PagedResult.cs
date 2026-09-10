namespace BuildingBlocks.Domain;

public class PaginationRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int Skip => (Page - 1) * PageSize;
    public int Take => PageSize;
    public string? SortColumn { get; init; }
    public string? SortDirection { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Texto a buscar en el título y la descripción, o nulo para no buscar nada.
    ///
    /// <b>Está aquí y no en cada módulo</b> porque buscar por texto es la misma operación en
    /// tareas, tickets y proyectos, y porque los endpoints ya construyen este objeto: repetir el
    /// parámetro por módulo daría tres nombres distintos para lo mismo, que es como el frontend
    /// acaba llamando `q` en un sitio y `search` en otro.
    ///
    /// <b>No hace falta quitarle tildes ni mayúsculas.</b> La base de datos usa la colación
    /// <c>utf8mb4_0900_ai_ci</c> —insensible a acentos y a caja— así que «AUTOMATICAS»,
    /// «automaticas» y «automáticas» encuentran lo mismo. Comprobado sobre los datos reales antes
    /// de escribir esto; normalizar en el cliente sería duplicar trabajo que la base ya hace.
    /// </summary>
    public string? Buscar { get; init; }

    /// <summary>
    /// El texto listo para usar: recortado, o nulo si no hay nada que buscar.
    ///
    /// Una cadena de espacios llega desde un formulario que se ha vaciado, y sin esto se
    /// convertiría en un <c>LIKE '%   %'</c> que no encuentra casi nada — una lista vacía sin
    /// motivo aparente.
    /// </summary>
    public string? TextoBuscado
        => string.IsNullOrWhiteSpace(Buscar) ? null : Buscar.Trim();
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    private PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
        => new(items, totalCount, page, pageSize);
}
