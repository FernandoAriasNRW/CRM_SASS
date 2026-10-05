using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Commands;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public class DeleteDocumentHandler(TimeProvider timeProvider, IDocumentRepository repository) 
    : IRequestHandler<DeleteDocumentCommand, Result>
{
    public async Task<Result> Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document == null)
            return Result.Failure("Document not found");

        document.Delete(timeProvider.GetUtcNow().UtcDateTime);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
