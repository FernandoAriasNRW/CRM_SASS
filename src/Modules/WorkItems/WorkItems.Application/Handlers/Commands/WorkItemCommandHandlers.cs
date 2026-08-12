using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Application.Commands;
using WorkItems.Domain.Entities;

namespace WorkItems.Application.Handlers.Commands;

public sealed class CreateTaskCommandHandler(
    ITaskRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateTaskCommand, WorkTask>
{
  public async Task<Result<WorkTask>> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
  {
    WorkTask task;
    try
    {
      task = WorkTask.Create(
          request.TenantId, request.ProjectId, request.Title, request.Description,
          request.AssigneeId, request.CreatedById, request.EstimatedHours, request.DueDate,
          request.Priority);
    }
    catch (InvalidOperationException ex) { return Result<WorkTask>.Failure(ex.Message); }

    await repository.AddAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<WorkTask>.Success(task);
  }
}

public sealed class MoveTaskCommandHandler(
    ITaskRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<MoveTaskCommand, bool>
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
    IUnitOfWork unitOfWork) : ICommandHandler<PatchTaskCommand, bool>
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

public sealed class DeleteTaskCommandHandler(
    ITaskRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteTaskCommand, bool>
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
