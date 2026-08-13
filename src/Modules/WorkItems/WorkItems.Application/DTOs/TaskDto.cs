namespace WorkItems.Application.DTOs;

public sealed record TaskDto(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    string Title,
    string Description,
    string Status,
    string Priority,
    Guid AssigneeId,
    Guid CreatedById,
    decimal EstimatedHours,
    DateOnly DueDate,
    /// <summary>Tarea de la que ésta es subtarea, o null si es de primer nivel.</summary>
    Guid? ParentTaskId = null,
    /// <summary>
    /// Progreso agregado del padre: cuántas subtareas tiene y cuántas están completadas.
    ///
    /// Se calcula en la consulta y no se guarda en la tarea a propósito: un contador
    /// denormalizado se desincroniza en cuanto alguien mueve o borra una subtarea por otra vía,
    /// y entonces la interfaz miente sin que nada falle.
    /// </summary>
    int SubtaskCount = 0,
    int CompletedSubtaskCount = 0,
    /// <summary>
    /// Cuántas tareas bloquean a ésta y a cuántas bloquea ella. Igual que el progreso de las
    /// subtareas, se cuentan en la consulta: es lo que permite marcar una tarjeta como bloqueada
    /// sin pedir las dependencias tarea por tarea.
    /// </summary>
    int BlockedByCount = 0,
    int BlocksCount = 0
);
