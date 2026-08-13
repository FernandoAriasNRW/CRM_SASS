using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Application.Commands;

namespace WorkItems.Application.Handlers.Commands;

public sealed class SetTaskRecurrenceCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<SetTaskRecurrenceCommand, bool>
{
  public async Task<Result<bool>> Handle(SetTaskRecurrenceCommand request, CancellationToken cancellationToken)
  {
    var task = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    if (task is null)
      return Result<bool>.Failure("Tarea no encontrada");

    // Sin fecha de arranque, la serie empieza en la fecha límite de la tarea: es la que el
    // usuario ya eligió, y pedirla otra vez sería preguntar dos veces lo mismo.
    var arranque = request.ProximaOcurrencia ?? task.DueDate;

    try { task.Repetir(request.Frecuencia, request.Intervalo, arranque, request.FechaFin); }
    catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

    await repository.UpdateAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

public sealed class ClearTaskRecurrenceCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<ClearTaskRecurrenceCommand, bool>
{
  public async Task<Result<bool>> Handle(ClearTaskRecurrenceCommand request, CancellationToken cancellationToken)
  {
    var task = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    if (task is null)
      return Result<bool>.Failure("Tarea no encontrada");

    task.DejarDeRepetir();

    await repository.UpdateAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}
