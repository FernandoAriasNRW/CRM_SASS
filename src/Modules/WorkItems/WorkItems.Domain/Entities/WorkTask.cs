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

    /// <summary>
    /// Responsable principal, o <see cref="Guid.Empty"/> si la tarea no tiene a nadie.
    ///
    /// No es una segunda fuente de verdad: quien esté aquí **figura siempre** en
    /// <see cref="Assignees"/>, y el agregado mantiene esa equivalencia. Se conserva el campo
    /// porque media aplicación lo usa —tableros, filtros, la vista de tabla— y porque «quién
    /// responde de esto» sigue siendo una pregunta con una sola respuesta útil.
    ///
    /// Ojo con el orden: en memoria el principal queda primero, pero al recargar desde la base
    /// la colección **no tiene orden garantizado**. Quien necesite saber quién es el principal
    /// compara con este campo, no con la posición.
    /// </summary>
    public Guid AssigneeId { get; private set; }

    private readonly List<TaskAssignee> _assignees = [];

    /// <summary>
    /// Todas las personas responsables, el principal incluido. Sin orden significativo al venir
    /// de la base: ver <see cref="AssigneeId"/>.
    /// </summary>
    public IReadOnlyList<TaskAssignee> Assignees => _assignees;

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

        // Una tarea que nace asignada aparece ya en el conjunto de responsables, no sólo en el
        // campo del principal: si no, la tarea tendría un principal que no figura entre sus
        // responsables y ninguna vista de las nuevas la encontraría.
        if (assigneeId != Guid.Empty)
            task._assignees.Add(new TaskAssignee(assigneeId));

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

    /// <summary>
    /// Cambia el responsable principal.
    ///
    /// Quien pasa a ser principal entra también en el conjunto si no estaba: es la invariante
    /// que sostiene que <see cref="AssigneeId"/> sea el primero de <see cref="Assignees"/> y no
    /// un dato paralelo. Asignar a <see cref="Guid.Empty"/> deja la tarea sin principal y vacía
    /// el conjunto, que es lo que significa «sin asignar».
    /// </summary>
    public void Assign(Guid assigneeId)
    {
        if (assigneeId == Guid.Empty)
        {
            _assignees.Clear();
            AssigneeId = Guid.Empty;
            RaiseDomainEvent(new TaskAssignedEvent(Id, TenantId, Guid.Empty));
            return;
        }

        _assignees.RemoveAll(a => a.UserId == assigneeId);
        _assignees.Insert(0, new TaskAssignee(assigneeId));
        AssigneeId = assigneeId;

        RaiseDomainEvent(new TaskAssignedEvent(Id, TenantId, assigneeId));
    }

    /// <summary>
    /// Añade una persona responsable sin tocar quién es el principal.
    ///
    /// Si la tarea no tenía a nadie, la primera que entra pasa a ser la principal: dejar el
    /// campo vacío con responsables dentro rompería la equivalencia.
    /// </summary>
    public void AddAssignee(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new InvalidOperationException(ReglasDeResponsables.IdentificadorInvalido);

        if (_assignees.Any(a => a.UserId == userId))
            throw new InvalidOperationException(ReglasDeResponsables.YaEsResponsable);

        _assignees.Add(new TaskAssignee(userId));

        if (AssigneeId == Guid.Empty)
            AssigneeId = userId;

        RaiseDomainEvent(new TaskAssigneeAddedEvent(Id, TenantId, userId));
    }

    /// <summary>
    /// Quita a una persona de los responsables.
    ///
    /// Si era la principal, **promociona a la siguiente** en lugar de dejar la tarea con un
    /// principal que ya no es responsable. Si era la última, la tarea queda sin asignar, que es
    /// un estado que el sistema ya admite.
    /// </summary>
    public void RemoveAssignee(Guid userId)
    {
        var quitados = _assignees.RemoveAll(a => a.UserId == userId);
        if (quitados == 0)
            throw new InvalidOperationException(ReglasDeResponsables.NoEsResponsable);

        if (AssigneeId == userId)
            AssigneeId = _assignees.Count > 0 ? _assignees[0].UserId : Guid.Empty;

        RaiseDomainEvent(new TaskAssigneeRemovedEvent(Id, TenantId, userId));
    }

    /// <summary>Si una persona figura entre los responsables.</summary>
    public bool EsResponsable(Guid userId) => _assignees.Any(a => a.UserId == userId);

    public static class ReglasDeResponsables
    {
        public const string IdentificadorInvalido = "El identificador de la persona no es válido";
        public const string YaEsResponsable = "Esa persona ya es responsable de la tarea";
        public const string NoEsResponsable = "Esa persona no es responsable de la tarea";
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
