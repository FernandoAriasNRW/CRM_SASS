using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Application.Commands;
using WorkItems.Application.DTOs;

namespace WorkItems.Application.Handlers.Commands;

/// <summary>
/// Alta, cambio y baja de puntos de la checklist. Handlers finos: el texto obligatorio, el largo
/// máximo y el cálculo de la posición son invariantes de la tarea y viven en el agregado.
/// </summary>
public sealed class AddChecklistItemCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<AddChecklistItemCommand, ChecklistItemDto>
{
  public async Task<Result<ChecklistItemDto>> Handle(AddChecklistItemCommand request, CancellationToken cancellationToken)
  {
    var task = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    if (task is null)
      return Result<ChecklistItemDto>.Failure("Tarea no encontrada");

    Domain.Entities.ChecklistItem punto;
    try { punto = task.AddChecklistItem(request.Texto); }
    catch (InvalidOperationException ex) { return Result<ChecklistItemDto>.Failure(ex.Message); }

    await repository.UpdateAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Result<ChecklistItemDto>.Success(
        new ChecklistItemDto(punto.Id, punto.Texto, punto.Hecho, punto.Posicion));
  }
}

public sealed class UpdateChecklistItemCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<UpdateChecklistItemCommand, bool>
{
  public async Task<Result<bool>> Handle(UpdateChecklistItemCommand request, CancellationToken cancellationToken)
  {
    var task = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    if (task is null)
      return Result<bool>.Failure("Tarea no encontrada");

    try { task.UpdateChecklistItem(request.ItemId, request.Hecho, request.Texto); }
    catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

    await repository.UpdateAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}

public sealed class RemoveChecklistItemCommandHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<RemoveChecklistItemCommand, bool>
{
  public async Task<Result<bool>> Handle(RemoveChecklistItemCommand request, CancellationToken cancellationToken)
  {
    var task = await repository.GetByIdAsync(request.TenantId, request.Id, cancellationToken);
    if (task is null)
      return Result<bool>.Failure("Tarea no encontrada");

    try { task.RemoveChecklistItem(request.ItemId); }
    catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

    await repository.UpdateAsync(task, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);
    return Result<bool>.Success(true);
  }
}
