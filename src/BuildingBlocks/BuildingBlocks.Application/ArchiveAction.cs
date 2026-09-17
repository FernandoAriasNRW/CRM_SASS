namespace BuildingBlocks.Application;

/// <summary>
/// Qué se le hace a algo para apartarlo de la vista, o para traerlo de vuelta.
///
/// Las cuatro acciones van juntas en un solo tipo porque son las cuatro esquinas de la misma
/// decisión y conviene verlas a la vez: archivar tiene su vuelta, borrar tiene la suya, y
/// ninguna de las dos parejas anula a la otra.
/// </summary>
public enum ArchiveAction
{
    /// <summary>Fuera de las listas, sigue existiendo, sin promesa de que desaparezca.</summary>
    Archive,

    /// <summary>De vuelta a las listas.</summary>
    Unarchive,

    /// <summary>A la papelera: borrado y recuperable.</summary>
    MoveToTrash,

    /// <summary>Fuera de la papelera, tal y como estaba —archivado incluido, si lo estaba—.</summary>
    RestoreFromTrash
}
