namespace BuildingBlocks.Application.Abstractions;

/// <summary>
/// Qué proyectos existen en una organización, para los módulos que cuelgan cosas de un proyecto.
///
/// **Toda tarea pertenece a un proyecto**, y el tablero de un proyecto es la forma de llegar a sus
/// tareas. Hasta ahora el identificador se guardaba sin comprobarlo: una tarea podía apuntar a un
/// proyecto inventado o de otra organización, y entonces no salía en el tablero de ningún
/// proyecto. Es un puerto por lo mismo que <see cref="ITagCatalog"/>: los proyectos viven en
/// Projects y ningún módulo referencia a otro. Lo implementa Projects.
/// </summary>
public interface IProjectCatalog
{
    /// <summary>
    /// Si el proyecto existe en la organización y está en uso: ni borrado ni archivado. Un
    /// proyecto archivado está cerrado, y colgarle trabajo nuevo lo escondería con él.
    /// </summary>
    Task<bool> ExistsAsync(Guid tenantId, Guid projectId, CancellationToken ct = default);
}
