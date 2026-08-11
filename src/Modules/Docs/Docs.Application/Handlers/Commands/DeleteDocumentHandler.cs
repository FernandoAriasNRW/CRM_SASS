using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Commands;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public class DeleteDocumentHandler(IDocumentRepository repository) 
    : IRequestHandler<DeleteDocumentCommand, Result>
{
    public async Task<Result> Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document == null)
            return Result.Failure("Document not found");

        document.Delete();
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
