using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Application.Commands;

namespace WorkItems.Application.Handlers.Commands;

/// <summary>
/// Alta y baja de responsables. Los handlers son finos a propósito: las reglas —sin duplicados,
/// el principal siempre dentro del conjunto, promoción al quitar al principal— son invariantes
/// de la tarea y viven en el agregado, no aquí.
/// </summary>
public sealed class AddTaskAssigneeCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<AddTaskAssigneeCommand, bool>
{
  public async Task<Result<bool>> Handle(AddTaskAssigneeCommand request, CancellationToken cancellationToken)
  {
    var task = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    if (task is null)
      return Result<bool>.Failure("Tarea no encontrada");

    try { task.AddAssignee(request.UserId); }
    catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

    await repository.UpdateAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

public sealed class RemoveTaskAssigneeCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<RemoveTaskAssigneeCommand, bool>
{
  public async Task<Result<bool>> Handle(RemoveTaskAssigneeCommand request, CancellationToken cancellationToken)
  {
    var task = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    if (task is null)
      return Result<bool>.Failure("Tarea no encontrada");

    try { task.RemoveAssignee(request.UserId); }
    catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

    await repository.UpdateAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}
