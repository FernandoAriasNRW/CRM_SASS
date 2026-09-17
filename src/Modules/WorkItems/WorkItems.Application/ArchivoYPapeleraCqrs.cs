using BuildingBlocks.Application;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions;
using WorkItems.Application.Abstractions.Repositories;

namespace WorkItems.Application;

/// <summary>
/// Archivar, desarchivar, tirar a la papelera y restaurar una tarea.
///
/// Un solo comando con una acción, y no cuatro comandos: las cuatro hacen lo mismo —cargar la
/// tarea, cambiarle un campo, guardar— y separarlas serían cuatro copias del mismo handler
/// esperando a divergir.
/// </summary>
public sealed record CambiarArchivoDeTareaCommand(
    Guid TenantId,
    Guid Id,
    ArchiveAction Accion) : ICommand<bool>;

public sealed class CambiarArchivoDeTareaHandler(
    ITaskRepository repository,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<CambiarArchivoDeTareaCommand, bool>
{
    public async Task<Result<bool>> Handle(CambiarArchivoDeTareaCommand request, CancellationToken ct)
    {
        // Se busca incluyendo lo oculto **a propósito**: restaurar algo de la papelera exige
        // encontrarlo, y el filtro global lo esconde. Con la búsqueda normal, «restaurar»
        // habría contestado siempre «tarea no encontrada».
        var tarea = await repository.GetIncluyendoOcultosAsync(request.TenantId, request.Id, ct);
        if (tarea is null)
            return Result<bool>.Failure("Tarea no encontrada");

        switch (request.Accion)
        {
            case ArchiveAction.Archive: tarea.Archivar(); break;
            case ArchiveAction.Unarchive: tarea.Desarchivar(); break;
            case ArchiveAction.MoveToTrash: tarea.MoveToTrash(); break;
            case ArchiveAction.RestoreFromTrash: tarea.RestoreFromTrash(); break;
        }

        await repository.UpdateAsync(tarea, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
