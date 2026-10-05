using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Application.Commands;
using WorkItems.Domain.Entities;
using WorkItems.Domain.Services;

namespace WorkItems.Application.Handlers.Commands;

/// <summary>
/// Añade una dependencia entre dos tareas.
///
/// Aquí viven las comprobaciones que hablan de otras filas: que las dos tareas existan y sean
/// del mismo proyecto, que la dependencia no esté ya registrada, y —la importante— que no
/// cierre un ciclo. La decisión del ciclo la toma <see cref="CycleDetector"/>, que es una
/// función pura; este handler sólo le trae las aristas.
/// </summary>
public sealed class AddTaskDependencyCommandHandler(
    ITaskRepository tasks,
    ITaskDependencyRepository dependencies,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<AddTaskDependencyCommand, bool>
{
  public async Task<Result<bool>> Handle(AddTaskDependencyCommand request, CancellationToken cancellationToken)
  {
    if (request.Id == request.DependsOnTaskId)
      return Result<bool>.Failure(TaskDependency.Rules.CannotBlockItself);

    var task = await tasks.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    var bloqueante = await tasks.GetByIdAsync(request.TenantId, request.DependsOnTaskId, cancellationToken);

    if (task is null || bloqueante is null)
      return Result<bool>.Failure(TaskDependency.Rules.TaskNotFound);

    if (task.ProjectId != bloqueante.ProjectId)
      return Result<bool>.Failure(TaskDependency.Rules.FromAnotherProject);

    var alreadyExists = await dependencies.GetAsync(request.TenantId, request.Id, request.DependsOnTaskId, cancellationToken);
    if (alreadyExists is not null)
      return Result<bool>.Failure(TaskDependency.Rules.AlreadyExists);

    var edges = await dependencies.GetProjectEdgesAsync(request.TenantId, task.ProjectId, cancellationToken);
    if (CycleDetector.WouldCloseCycle(edges, request.Id, request.DependsOnTaskId))
      return Result<bool>.Failure(TaskDependency.Rules.WouldCreateCycle);

    TaskDependency dependency;
    try { dependency = TaskDependency.Create(request.TenantId, request.Id, request.DependsOnTaskId); }
    catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

    await dependencies.AddAsync(dependency, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<bool>.Success(true);
  }
}

public sealed class RemoveTaskDependencyCommandHandler(
    ITaskDependencyRepository dependencies,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<RemoveTaskDependencyCommand, bool>
{
  public async Task<Result<bool>> Handle(RemoveTaskDependencyCommand request, CancellationToken cancellationToken)
  {
    var dependency = await dependencies.GetAsync(request.TenantId, request.Id, request.DependsOnTaskId, cancellationToken);
    if (dependency is null)
      return Result<bool>.Failure("La dependencia no existe");

    dependency.MarkAsRemoved();
    dependencies.Remove(dependency);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<bool>.Success(true);
  }
}
