using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public record UpdatePageCommand(Guid PageId, string Title, string Content) : IRequest<Result>, IAuthorizeEntity
{
    // La página no lleva el documento, así que se comprueba el nivel sobre los documentos en general.
    public string EntityType => "Document";
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";
}

public class UpdatePageHandler(
    IDocumentRepository documentRepository,
    Menciones.ActualizadorDeMenciones menciones,
    IUserContext usuario) : IRequestHandler<UpdatePageCommand, Result>
{
    public async Task<Result> Handle(UpdatePageCommand request, CancellationToken cancellationToken)
    {
        var page = await documentRepository.GetPageByIdAsync(request.PageId, cancellationToken);
        if (page == null)
            return Result.Failure("The page was not found.");

        page.UpdateContent(request.Title, request.Content);

        await documentRepository.SaveChangesAsync(cancellationToken);

        // Las menciones se vuelven a leer **del contenido que se acaba de guardar**, no de lo que
        // mande el cliente. Es lo que impide que el documento diga una cosa y la tabla otra:
        // alguien borra la mención del texto y la tarea seguiría enseñando el documento para
        // siempre.
        //
        // Va después de guardar y no en la misma transacción a propósito: si esto fallara, se
        // perdería la actualización del índice de menciones, no el contenido de la persona. El
        // índice se rehace al siguiente guardado; el texto no se recupera.
        await menciones.ActualizarAsync(
            usuario.TenantId, page.DocumentId, page.Id, request.Content, cancellationToken);

        return Result.Success();
    }
}
