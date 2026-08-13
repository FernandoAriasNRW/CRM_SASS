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
    int BlocksCount = 0,
    /// <summary>
    /// Todas las personas responsables, la principal primero. <c>AssigneeId</c> es la principal
    /// y se conserva porque media aplicación lo usa; esta lista es el conjunto completo.
    /// </summary>
    IReadOnlyList<Guid>? Assignees = null,
    /// <summary>
    /// Progreso de la checklist. Se cuenta en la consulta, como el de las subtareas: los puntos
    /// completos se piden aparte cuando hacen falta, que es sólo al abrir el detalle.
    /// </summary>
    int ChecklistTotal = 0,
    int ChecklistDone = 0,
    /// <summary>Cada cuánto se repite, si se repite. Sólo lo lleva la tarea plantilla.</summary>
    RecurrenceDto? Recurrence = null
);

/// <summary>El patrón de repetición tal como lo ve la interfaz.</summary>
public sealed record RecurrenceDto(
    string Frecuencia,
    int Intervalo,
    DateOnly ProximaOcurrencia,
    DateOnly? FechaFin
);
