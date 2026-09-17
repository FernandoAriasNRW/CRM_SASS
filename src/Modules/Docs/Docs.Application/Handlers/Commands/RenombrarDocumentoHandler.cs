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
public record RenombrarDocumentoCommand(Guid DocumentId, string Title, string? Description)
    : IRequest<Result>, IAuthorizeEntity
{
    public string EntityType => "Document";
    public Guid EntityId => DocumentId;
    public string RequiredPermission => "Write";
}

public class RenombrarDocumentoHandler(IDocumentRepository repository)
    : IRequestHandler<RenombrarDocumentoCommand, Result>
{
    public async Task<Result> Handle(RenombrarDocumentoCommand request, CancellationToken cancellationToken)
    {
        var titulo = (request.Title ?? string.Empty).Trim();
        if (titulo.Length == 0)
            return Result.Failure("El documento necesita un título.");

        if (titulo.Length > 255)
            return Result.Failure("El título no puede pasar de 255 caracteres.");

        var documento = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (documento is null)
            return Result.Failure("El documento no existe.");

        // La descripción es opcional y se conserva cuando no viene. La pantalla renombra desde un
        // campo que sólo edita el título; mandar `null` y que eso borrara la descripción sería
        // perder un dato que nadie pidió tocar.
        documento.Update(titulo, request.Description ?? documento.Description);

        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
