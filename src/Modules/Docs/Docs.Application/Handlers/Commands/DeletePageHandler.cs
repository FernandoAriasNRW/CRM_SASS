using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Commands;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public class DeletePageHandler(IDocumentRepository repository) 
    : IRequestHandler<DeletePageCommand, Result>
{
    public async Task<Result> Handle(DeletePageCommand request, CancellationToken cancellationToken)
    {
        var page = await repository.GetPageByIdAsync(request.PageId, cancellationToken);
        if (page == null)
            return Result.Failure("Page not found");

        page.Delete();
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
