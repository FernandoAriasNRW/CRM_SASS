using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using MediatR;

namespace Docs.Application.Handlers.Commands;

/// <summary>
/// Cambia el título y la descripción de un documento.
///
/// <b>No existía ningún endpoint para esto.</b> El módulo sólo publicaba borrar documento, borrar
/// página y actualizar página, así que el campo de título que la pantalla enseña arriba del todo
/// no podía funcionar de ninguna manera: escribiera lo que escribiera, el documento seguía
/// llamándose igual. <c>Document.Update</c> llevaba escrito en el dominio desde el principio sin
/// que lo llamara nadie.
/// </summary>
public record RenameDocumentCommand(Guid DocumentId, string Title, string? Description)
    : IRequest<Result>, IAuthorizeEntity
{
    public string EntityType => "Document";
    public Guid EntityId => DocumentId;
    public string RequiredPermission => "Write";
}

public class RenameDocumentHandler(IDocumentRepository repository)
    : IRequestHandler<RenameDocumentCommand, Result>
{
    public async Task<Result> Handle(RenameDocumentCommand request, CancellationToken cancellationToken)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0)
            return Result.Failure("El documento necesita un título.");

        if (title.Length > 255)
            return Result.Failure("El título no puede pasar de 255 caracteres.");

        var document = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document is null)
            return Result.Failure("El documento no existe.");

        // La descripción es opcional y se conserva cuando no viene. La pantalla renombra desde un
        // campo que sólo edita el título; mandar `null` y que eso borrara la descripción sería
        // perder un dato que nadie pidió tocar.
        document.Update(title, request.Description ?? document.Description);

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
