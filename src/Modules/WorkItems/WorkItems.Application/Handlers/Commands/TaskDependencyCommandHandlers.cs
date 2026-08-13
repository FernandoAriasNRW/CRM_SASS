using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Application.Commands;
using WorkItems.Domain.Entities;
using WorkItems.Domain.Servicios;

namespace WorkItems.Application.Handlers.Commands;

/// <summary>
/// Añade una dependencia entre dos tareas.
///
/// Aquí viven las comprobaciones que hablan de otras filas: que las dos tareas existan y sean
/// del mismo proyecto, que la dependencia no esté ya registrada, y —la importante— que no
/// cierre un ciclo. La decisión del ciclo la toma <see cref="DetectorDeCiclos"/>, que es una
/// función pura; este handler sólo le trae las aristas.
/// </summary>
public sealed class AddTaskDependencyCommandHandler(
    ITaskRepository tasks,
    ITaskDependencyRepository dependencias,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<AddTaskDependencyCommand, bool>
{
  public async Task<Result<bool>> Handle(AddTaskDependencyCommand request, CancellationToken cancellationToken)
  {
    if (request.Id == request.DependsOnTaskId)
      return Result<bool>.Failure(TaskDependency.Reglas.NoPuedeBloquearseASiMisma);

    var tarea = await tasks.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    var bloqueante = await tasks.GetByIdAsync(request.TenantId, request.DependsOnTaskId, cancellationToken);

    if (tarea is null || bloqueante is null)
      return Result<bool>.Failure(TaskDependency.Reglas.TareaNoExiste);

    if (tarea.ProjectId != bloqueante.ProjectId)
      return Result<bool>.Failure(TaskDependency.Reglas.DeOtroProyecto);

    var yaExiste = await dependencias.GetAsync(request.TenantId, request.Id, request.DependsOnTaskId, cancellationToken);
    if (yaExiste is not null)
      return Result<bool>.Failure(TaskDependency.Reglas.YaExiste);

    var aristas = await dependencias.GetAristasDelProyectoAsync(request.TenantId, tarea.ProjectId, cancellationToken);
    if (DetectorDeCiclos.CerrariaUnCiclo(aristas, request.Id, request.DependsOnTaskId))
      return Result<bool>.Failure(TaskDependency.Reglas.CrearariaUnCiclo);

    TaskDependency dependencia;
    try { dependencia = TaskDependency.Create(request.TenantId, request.Id, request.DependsOnTaskId); }
    catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

    await dependencias.AddAsync(dependencia, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<bool>.Success(true);
  }
}

public sealed class RemoveTaskDependencyCommandHandler(
    ITaskDependencyRepository dependencias,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<RemoveTaskDependencyCommand, bool>
{
  public async Task<Result<bool>> Handle(RemoveTaskDependencyCommand request, CancellationToken cancellationToken)
  {
    var dependencia = await dependencias.GetAsync(request.TenantId, request.Id, request.DependsOnTaskId, cancellationToken);
    if (dependencia is null)
      return Result<bool>.Failure("La dependencia no existe");

    dependencia.MarcarComoRetirada();
    dependencias.Remove(dependencia);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<bool>.Success(true);
  }
}
