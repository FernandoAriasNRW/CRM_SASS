using BuildingBlocks.Domain.Primitives;
using WorkItems.Domain.Events;
using WorkItems.Domain.ValueObjects;
using TaskStatus = WorkItems.Domain.ValueObjects.TaskStatus;

namespace WorkItems.Domain.Entities;

public sealed class WorkTask : AggregateRoot, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public TaskTitle Title { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public TaskStatus Status { get; private set; } = null!;
    public TaskPriority Priority { get; private set; } = null!;

    /// <summary>
    /// Tarea de la que ésta es subtarea, o <c>null</c> si es de primer nivel.
    /// </summary>
    public Guid? ParentTaskId { get; private set; }

    public Guid AssigneeId { get; private set; }
    public Guid CreatedById { get; private set; }
    public decimal EstimatedHours { get; private set; }
    public DateOnly DueDate { get; private set; }
    public List<Guid> TagIds { get; private set; } = new();

    private WorkTask() { }

    public static WorkTask Create(
        Guid tenantId,
        Guid projectId,
        string title,
        string description,
        Guid assigneeId,
        Guid createdById,
        decimal estimatedHours,
        DateOnly dueDate,
        string? priority = null,
        Guid? parentTaskId = null)
    {
        if (priority is not null && !TaskPriority.Existe(priority))
            throw new InvalidOperationException($"La prioridad '{priority}' no existe");

        if (parentTaskId == Guid.Empty)
            throw new InvalidOperationException("El identificador de la tarea padre no es válido");

        var task = new WorkTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            Title = TaskTitle.Create(title).Value!,
            Description = description,
            Status = TaskStatus.ToDo,
            Priority = priority is null ? TaskPriority.PorDefecto : TaskPriority.Desde(priority),
            ParentTaskId = parentTaskId,
            AssigneeId = assigneeId,
            CreatedById = createdById,
            EstimatedHours = estimatedHours,
            DueDate = dueDate
        };

        task.RaiseDomainEvent(new TaskCreatedEvent(task.Id, tenantId, projectId, assigneeId));

        return task;
    }

    /// <summary>
    /// Cambia el estado de la tarea.
    ///
    /// Se admite cualquier estado existente, sin restringir desde cuál se viene: qué
    /// movimiento tiene sentido lo decide quien gestiona el trabajo. Sólo se rechaza un
    /// estado que no exista, que sería un dato corrupto.
    /// </summary>
    public void Move(string newStatus)
    {
        if (!TaskStatus.Existe(newStatus))
            throw new InvalidOperationException($"El estado '{newStatus}' no existe");

        var oldStatus = Status;
        Status = new TaskStatus(newStatus, newStatus);

        RaiseDomainEvent(new TaskStatusChangedEvent(Id, TenantId, ProjectId, oldStatus.Value.ToString(), newStatus));
    }

    /// <summary>
    /// Cambia la prioridad de la tarea.
    ///
    /// Igual que con el estado, se admite cualquier prioridad existente y en cualquier
    /// sentido: subir o bajar una tarea es de quien gestiona el trabajo. Sólo se rechaza una
    /// prioridad que no exista.
    ///
    /// Repriorizar a la que ya tiene no emite evento: sin cambio real no hay nada que
    /// contar, y un evento vacío haría trabajar de más a las automatizaciones de la 4D.
    /// </summary>
    public void Reprioritize(string newPriority)
    {
        if (!TaskPriority.Existe(newPriority))
            throw new InvalidOperationException($"La prioridad '{newPriority}' no existe");

        if (Priority.Value == newPriority)
            return;

        var oldPriority = Priority;
        Priority = TaskPriority.Desde(newPriority);

        RaiseDomainEvent(new TaskPriorityChangedEvent(Id, TenantId, ProjectId, oldPriority.Value, newPriority));
    }

    /// <summary>Si esta tarea es subtarea de otra.</summary>
    public bool EsSubtarea => ParentTaskId.HasValue;

    /// <summary>
    /// Cuelga esta tarea de otra, o la desliga si se pasa <c>null</c>.
    ///
    /// El anidamiento está limitado a **un nivel**: hay tareas y subtareas, y no subtareas de
    /// subtareas. Es lo que hace que el progreso del padre sea una cuenta y no un recorrido de
    /// árbol, y evita de raíz los ciclos, porque un padre no puede tener padre.
    ///
    /// Aquí sólo se comprueba lo que el agregado puede ver: que una tarea no sea su propio
    /// padre. Las otras dos reglas —que el padre no sea ya subtarea, y que la tarea que se
    /// subordina no tenga subtareas propias— necesitan consultar otras filas, así que las
    /// aplica el handler antes de llamar. Están enumeradas en <see cref="ReglasDeAnidamiento"/>
    /// para que no se dupliquen a medias.
    /// </summary>
    public void Reparent(Guid? parentTaskId)
    {
        if (parentTaskId == Id)
            throw new InvalidOperationException("Una tarea no puede ser subtarea de sí misma");

        if (parentTaskId == Guid.Empty)
            throw new InvalidOperationException("El identificador de la tarea padre no es válido");

        if (ParentTaskId == parentTaskId)
            return;

        var anterior = ParentTaskId;
        ParentTaskId = parentTaskId;

        RaiseDomainEvent(new TaskParentChangedEvent(Id, TenantId, ProjectId, anterior, parentTaskId));
    }

    /// <summary>
    /// Las reglas de anidamiento, en un solo sitio y en el lenguaje del dominio, para que el
    /// handler que las aplica no las reinvente y los mensajes de error sean los mismos.
    /// </summary>
    public static class ReglasDeAnidamiento
    {
        /// <summary>Niveles admitidos: la tarea y sus subtareas.</summary>
        public const int ProfundidadMaxima = 2;

        public const string PadreNoExiste = "La tarea padre no existe";
        public const string PadreEsSubtarea = "Una subtarea no puede tener subtareas: el anidamiento admite un solo nivel";
        public const string TieneSubtareas = "Una tarea con subtareas no puede convertirse en subtarea de otra";
        public const string PadreDeOtroProyecto = "La tarea padre pertenece a otro proyecto";
    }

    public void Assign(Guid assigneeId)
    {
        AssigneeId = assigneeId;
        RaiseDomainEvent(new TaskAssignedEvent(Id, TenantId, assigneeId));
    }

    public void AddTag(Guid tagId)
    {
        if (!TagIds.Contains(tagId))
        {
            TagIds.Add(tagId);
            // Optionally raise domain event
        }
    }

    public void RemoveTag(Guid tagId)
    {
        if (TagIds.Contains(tagId))
        {
            TagIds.Remove(tagId);
        }
    }
}
