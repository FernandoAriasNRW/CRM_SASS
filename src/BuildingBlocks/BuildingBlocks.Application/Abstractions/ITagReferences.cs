namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Un módulo que guarda ids de etiquetas y sabe soltarlos cuando la etiqueta se borra.
///
/// Lo implementa cada módulo que etiqueta (tareas, tickets, proyectos, informes, dashboards) y lo
/// usa Tags al borrar, pidiendo todas las implementaciones registradas. Así Tags no conoce a
/// ninguno, y un módulo nuevo que etiquete sólo tiene que registrar la suya.
///
/// Sin esto, borrar una etiqueta dejaría su id colgando en cada tarea y ticket que la llevaba.
/// </summary>
public interface ITagReferences
{
    /// <returns>Cuántas filas llevaban la etiqueta.</returns>
    Task<int> RemoveTagAsync(Guid tenantId, Guid tagId, CancellationToken ct = default);
}
