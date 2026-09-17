using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Projects.Application.Abstractions;
using Projects.Application.Abstractions.Repositories;

namespace Projects.Application;

/// <summary>
/// Archivar y desarchivar un proyecto, y sacarlo de la papelera.
///
/// La papelera del proyecto ya existía —<c>Delete</c> y <c>Restore</c> en el agregado, con su
/// evento de dominio— así que aquí no se duplica: enviar a la papelera sigue siendo
/// <c>DeleteProjectCommand</c>. Lo que faltaba era el archivo, y una forma de restaurar desde
/// la pantalla de la papelera.
/// </summary>
public sealed record CambiarArchivoDeProyectoCommand(
    Guid TenantId,
    Guid Id,
    Guid ActorId,
    ArchiveAction Accion) : ICommand<bool>, IAuthorizeEntity
{
    public string EntityType => "Project";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}

public sealed class CambiarArchivoDeProyectoHandler(
    IProjectRepository repository,
    IProjectsUnitOfWork unitOfWork) : ICommandHandler<CambiarArchivoDeProyectoCommand, bool>
{
    public async Task<Result<bool>> Handle(CambiarArchivoDeProyectoCommand request, CancellationToken ct)
    {
        var proyecto = await repository.GetByIdAsync(request.TenantId, request.Id, includeDeleted: true, ct);
        if (proyecto is null)
            return Result<bool>.Failure("Proyecto no encontrado");

        try
        {
            switch (request.Accion)
            {
                case ArchiveAction.Archive: proyecto.Archivar(); break;
                case ArchiveAction.Unarchive: proyecto.Desarchivar(); break;

                // El agregado lanza si ya está borrado o si no lo está. Se traduce a un fallo
                // con mensaje en vez de dejar salir la excepción: pulsar dos veces «restaurar»
                // no es un error del programa, es una pantalla con datos de hace un segundo.
                case ArchiveAction.MoveToTrash: proyecto.Delete(request.ActorId); break;
                case ArchiveAction.RestoreFromTrash: proyecto.Restore(); break;
            }
        }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

        await repository.UpdateAsync(proyecto, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
