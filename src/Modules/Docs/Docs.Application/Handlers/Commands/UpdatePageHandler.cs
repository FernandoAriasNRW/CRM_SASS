using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public record UpdatePageCommand(Guid PageId, string Title, string Content) : IRequest<Result>;

public class UpdatePageHandler(IDocumentRepository documentRepository) : IRequestHandler<UpdatePageCommand, Result>
{
    public async Task<Result> Handle(UpdatePageCommand request, CancellationToken cancellationToken)
    {
        var page = await documentRepository.GetPageByIdAsync(request.PageId, cancellationToken);
        if (page == null)
            return Result.Failure("The page was not found.");

        page.UpdateContent(request.Title, request.Content);
        
        await documentRepository.SaveChangesAsync(cancellationToken);
        
        return Result.Success();
    }
}
