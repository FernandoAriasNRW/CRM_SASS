namespace Tags.Domain.ValueObjects;

/// <summary>
/// Las categorías que trae el producto. Son valores guardados en <c>Tags.Category</c>: cambiar
/// uno exige migrar las filas en el mismo cambio.
///
/// Además de éstas, cada organización puede crear las suyas (<see cref="Entities.CustomTagCategory"/>).
///
/// <b>No hay categoría de prioridad</b> a propósito: tareas y tickets ya tienen un campo de
/// prioridad, y una etiqueta «Baja» en un ticket de prioridad alta sería contradecirlo.
/// </summary>
public static class TagCategory
{
    /// <summary>Una por equipo; se crea sola al nacer el equipo.</summary>
    public const string Team = "Team";

    /// <summary>Una por proyecto; se crea sola al nacer el proyecto.</summary>
    public const string Project = "Project";

    public const string Milestone = "Milestone";
    public const string Business = "Business";
    public const string Security = "Security";

    /// <summary>Qué clase de trabajo es: funcionalidad, bug, mejora… Las tareas no tienen otro campo para esto.</summary>
    public const string WorkType = "WorkType";

    /// <summary>En qué fase del ciclo de desarrollo está: requisitos, diseño, implementación…</summary>
    public const string DevelopmentPhase = "DevelopmentPhase";

    public static readonly IReadOnlyList<string> All =
        [Team, Project, Milestone, Business, Security, WorkType, DevelopmentPhase];

    public static bool IsBuiltIn(string? category) => category is not null && All.Contains(category);

    /// <summary>
    /// Las que sólo rellena el sistema. Crear a mano una etiqueta de equipo que no corresponde a
    /// ningún equipo sería una etiqueta huérfana desde el primer día.
    /// </summary>
    public static bool IsAutomatic(string? category) => category is Team or Project;
}
