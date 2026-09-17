using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Application.Abstractions;
using Projects.Application.Abstractions.Repositories;

namespace Projects.Application;

public sealed class ChangeProjectArchiveStateHandler(
    IProjectRepository repository,
    IProjectsUnitOfWork unitOfWork) : ICommandHandler<ChangeProjectArchiveStateCommand, bool>
{
    public async Task<Result<bool>> Handle(ChangeProjectArchiveStateCommand request, CancellationToken ct)
    {
        var project = await repository.GetByIdAsync(request.TenantId, request.Id, includeDeleted: true, ct);
        if (project is null)
            return Result<bool>.Failure("Proyecto no encontrado");

        try
        {
            switch (request.Action)
            {
                case ArchiveAction.Archive: project.Archive(); break;
                case ArchiveAction.Unarchive: project.Unarchive(); break;

                // El agregado lanza si ya está borrado o si no lo está. Se traduce a un fallo
                // con mensaje en vez de dejar salir la excepción: pulsar dos veces «restaurar»
                // no es un error del programa, es una pantalla con datos de hace un segundo.
                case ArchiveAction.MoveToTrash: project.Delete(request.ActorId); break;
                case ArchiveAction.RestoreFromTrash: project.Restore(); break;
            }
        }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

        await repository.UpdateAsync(project, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
