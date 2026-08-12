using BuildingBlocks.Application.Abstractions;
using WorkItems.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Application.Commands;
using WorkItems.Domain.Entities;

namespace WorkItems.Application.Handlers.Commands;

public sealed class CreateTaskCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<CreateTaskCommand, WorkTask>
{
  public async Task<Result<WorkTask>> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
  {
    // Si nace como subtarea, el padre tiene que existir, ser del mismo proyecto y no ser él
    // mismo una subtarea. Se comprueba antes de crear para no dejar nada a medias.
    if (request.ParentTaskId.HasValue)
    {
      var padre = await repository.GetByIdAsync(request.TenantId, request.ParentTaskId.Value, cancellationToken);

      if (padre is null)
        return Result<WorkTask>.Failure(WorkTask.ReglasDeAnidamiento.PadreNoExiste);

      if (padre.EsSubtarea)
        return Result<WorkTask>.Failure(WorkTask.ReglasDeAnidamiento.PadreEsSubtarea);

      if (padre.ProjectId != request.ProjectId)
        return Result<WorkTask>.Failure(WorkTask.ReglasDeAnidamiento.PadreDeOtroProyecto);
    }

    WorkTask task;
    try
    {
      task = WorkTask.Create(
          request.TenantId, request.ProjectId, request.Title, request.Description,
          request.AssigneeId, request.CreatedById, request.EstimatedHours, request.DueDate,
          request.Priority, request.ParentTaskId);
    }
    catch (InvalidOperationException ex) { return Result<WorkTask>.Failure(ex.Message); }

    await repository.AddAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<WorkTask>.Success(task);
  }
}

public sealed class MoveTaskCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<MoveTaskCommand, bool>
{
  public async Task<Result<bool>> Handle(MoveTaskCommand request, CancellationToken cancellationToken)
  {
    var task = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    if (task is null)
      return Result<bool>.Failure("Tarea no encontrada");

    if (request.ActorRole != "Admin" && task.AssigneeId != request.ActorId)
      return Result<bool>.Failure("No tiene permisos para mover esta tarea");

    try { task.Move(request.NewStatus); }
    catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

    await repository.UpdateAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

public sealed class PatchTaskCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<PatchTaskCommand, bool>
{
  public async Task<Result<bool>> Handle(PatchTaskCommand request, CancellationToken cancellationToken)
  {
    var task = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    if (task is null)
      return Result<bool>.Failure("Tarea no encontrada");

    if (request.AssigneeId.HasValue)
      task.Assign(request.AssigneeId.Value);

    if (!string.IsNullOrEmpty(request.Status))
    {
      try { task.Move(request.Status); }
      catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }
    }

    if (!string.IsNullOrEmpty(request.Priority))
    {
      try { task.Reprioritize(request.Priority); }
      catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }
    }

    await repository.UpdateAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

/// <summary>
/// Cuelga una tarea de otra o la desliga.
///
/// Aquí viven las dos reglas de anidamiento que el agregado no puede comprobar solo, porque
/// hablan de otras filas: que el padre no sea ya una subtarea, y que la tarea que se subordina
/// no tenga subtareas propias. La tercera —no ser su propio padre— la aplica el dominio.
/// </summary>
public sealed class ReparentTaskCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<ReparentTaskCommand, bool>
{
  public async Task<Result<bool>> Handle(ReparentTaskCommand request, CancellationToken cancellationToken)
  {
    var task = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    if (task is null)
      return Result<bool>.Failure("Tarea no encontrada");

    if (request.ParentTaskId.HasValue)
    {
      var padre = await repository.GetByIdAsync(request.TenantId, request.ParentTaskId.Value, cancellationToken);

      if (padre is null)
        return Result<bool>.Failure(WorkTask.ReglasDeAnidamiento.PadreNoExiste);

      if (padre.EsSubtarea)
        return Result<bool>.Failure(WorkTask.ReglasDeAnidamiento.PadreEsSubtarea);

      if (padre.ProjectId != task.ProjectId)
        return Result<bool>.Failure(WorkTask.ReglasDeAnidamiento.PadreDeOtroProyecto);

      var subtareasPropias = await repository.CountSubtasksAsync(request.TenantId, task.Id, cancellationToken);
      if (subtareasPropias > 0)
        return Result<bool>.Failure(WorkTask.ReglasDeAnidamiento.TieneSubtareas);
    }

    try { task.Reparent(request.ParentTaskId); }
    catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

    await repository.UpdateAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

public sealed class DeleteTaskCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<DeleteTaskCommand, bool>
{
  public async Task<Result<bool>> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
  {
    var task = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    if (task is null)
      return Result<bool>.Failure("Tarea no encontrada");

    await repository.UpdateAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}
