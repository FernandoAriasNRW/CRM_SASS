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
        var tarea = await repository.GetIncludingHiddenAsync(request.TenantId, request.Id, ct);
        if (tarea is null)
            return Result<bool>.Failure("Tarea no encontrada");

        switch (request.Action)
        {
            case ArchiveAction.Archive: tarea.Archive(); break;
            case ArchiveAction.Unarchive: tarea.Unarchive(); break;
            case ArchiveAction.MoveToTrash: tarea.MoveToTrash(); break;
            case ArchiveAction.RestoreFromTrash: tarea.RestoreFromTrash(); break;
        }

        await repository.UpdateAsync(tarea, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
