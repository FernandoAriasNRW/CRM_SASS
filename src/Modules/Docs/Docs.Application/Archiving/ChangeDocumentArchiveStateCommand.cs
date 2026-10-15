using BuildingBlocks.Application;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;

namespace Docs.Application.Archiving;

/// <summary>
/// Archivar, desarchivar y sacar de la papelera un documento. Como en tareas, tickets y proyectos:
/// un solo comando con la acción. Mandar a la papelera sigue siendo <c>DELETE</c>.
///
/// El documento tenía la columna de archivado desde que se modeló el concepto, y nada la usaba.
/// </summary>
public sealed record ChangeDocumentArchiveStateCommand(Guid TenantId, Guid Id, ArchiveAction Action)
    : ICommand<bool>, IAuthorizeEntity
{
    public string EntityType => "Document";
    public Guid EntityId => Id;
    public string RequiredPermission => "Write";
}

public sealed class ChangeDocumentArchiveStateHandler(TimeProvider timeProvider, IDocumentRepository repository)
    : ICommandHandler<ChangeDocumentArchiveStateCommand, bool>
{
    public async Task<Result<bool>> Handle(ChangeDocumentArchiveStateCommand request, CancellationToken ct)
    {
        // Incluyendo lo escondido a propósito: con la búsqueda normal, desarchivar o restaurar
        // contestarían siempre «no existe».
        var document = await repository.GetIncludingHiddenAsync(request.TenantId, request.Id, ct);
        if (document is null)
            return Result<bool>.Failure("Documento no encontrado");

        switch (request.Action)
        {
            case ArchiveAction.Archive: document.Archive(timeProvider.GetUtcNow().UtcDateTime); break;
            case ArchiveAction.Unarchive: document.Unarchive(); break;
            case ArchiveAction.MoveToTrash: document.Delete(timeProvider.GetUtcNow().UtcDateTime); break;
            case ArchiveAction.RestoreFromTrash: document.RestoreFromTrash(); break;
        }

        await repository.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
