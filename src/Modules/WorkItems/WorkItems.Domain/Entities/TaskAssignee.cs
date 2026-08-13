namespace WorkItems.Domain.Entities;

/// <summary>
/// Una persona responsable de una tarea.
///
/// Es una colección **propiedad de <see cref="WorkTask"/>**, no una entidad suelta: las reglas
/// que la gobiernan —sin duplicados, y el responsable principal siempre dentro del conjunto—
/// son invariantes de la tarea, y sólo el agregado que las contiene puede garantizarlas. Una
/// tabla aparte con su propio repositorio permitiría dejar la tarea con un principal que no
/// figura entre sus responsables, y nadie se enteraría.
/// </summary>
public sealed class TaskAssignee
{
    public Guid UserId { get; private set; }

    private TaskAssignee() { }

    internal TaskAssignee(Guid userId) => UserId = userId;
}
