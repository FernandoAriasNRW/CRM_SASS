using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Annotations;

public sealed class CreateAnnotationHandler(IDocumentRepository repository)
    : IRequestHandler<CreateAnnotationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateAnnotationCommand request, CancellationToken cancellationToken)
    {
        var page = await repository.GetPageByIdAsync(request.PageId, cancellationToken);
        if (page is null)
            return Result<Guid>.Failure("La página no existe.");

        // El documento sale de la página y no de lo que mande el cliente: si viniera de fuera,
        // una anotación podría quedar colgada de un documento que no es el suyo y el panel la
        // buscaría donde no está.
        var annotation = DocumentAnnotation.Create(
            request.TenantId, page.DocumentId, request.PageId, request.CreatedBy, request.QuotedText);

        await repository.AddAnnotationAsync(annotation, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(annotation.Id);
    }
}
