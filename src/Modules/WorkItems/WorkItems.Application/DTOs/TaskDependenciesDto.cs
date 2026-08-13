namespace WorkItems.Application.DTOs;

/// <summary>Una tarea vista desde el panel de dependencias: lo justo para pintarla.</summary>
public sealed record TaskDependencyRefDto(Guid Id, string Title, string Status, string Priority);

/// <summary>
/// Las dependencias de una tarea en las dos direcciones.
///
/// «Bloqueada por» es lo que impide avanzar; «bloquea a» es la consecuencia de no avanzar. Es
/// la misma relación mirada de los dos lados, y en la base sólo se guarda una vez.
/// </summary>
public sealed record TaskDependenciesDto(
    IReadOnlyList<TaskDependencyRefDto> BloqueadaPor,
    IReadOnlyList<TaskDependencyRefDto> BloqueaA
);
