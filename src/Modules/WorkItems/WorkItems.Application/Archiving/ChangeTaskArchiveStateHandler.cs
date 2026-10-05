using BuildingBlocks.Application;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions;
using WorkItems.Application.Abstractions.Repositories;

namespace WorkItems.Application;

public sealed class ChangeTaskArchiveStateHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<ChangeTaskArchiveStateCommand, bool>
{
    public async Task<Result<bool>> Handle(ChangeTaskArchiveStateCommand request, CancellationToken ct)
    {
        // Se busca incluyendo lo oculto **a propósito**: restaurar algo de la papelera exige
        // encontrarlo, y el filtro global lo esconde. Con la búsqueda normal, «restaurar»
        // habría contestado siempre «tarea no encontrada».
        var task = await repository.GetIncludingHiddenAsync(request.TenantId, request.Id, ct);
        if (task is null)
            return Result<bool>.Failure("Tarea no encontrada");

        switch (request.Action)
        {
            case ArchiveAction.Archive: task.Archive(); break;
            case ArchiveAction.Unarchive: task.Unarchive(); break;
            case ArchiveAction.MoveToTrash: task.MoveToTrash(); break;
            case ArchiveAction.RestoreFromTrash: task.RestoreFromTrash(); break;
        }

        await repository.UpdateAsync(task, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
