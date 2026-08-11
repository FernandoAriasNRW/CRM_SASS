using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public record CreatePageCommand(Guid DocumentId, Guid? ParentPageId, string Title) : IRequest<Result<Guid>>;

public class CreatePageHandler(IDocumentRepository documentRepository) : IRequestHandler<CreatePageCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePageCommand request, CancellationToken cancellationToken)
    {
        var document = await documentRepository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document == null)
            return Result<Guid>.Failure("The document was not found.");

        var order = document.Pages.Count;
        if (request.ParentPageId.HasValue)
        {
            var parentPage = await documentRepository.GetPageByIdAsync(request.ParentPageId.Value, cancellationToken);
            if (parentPage == null)
                return Result<Guid>.Failure("The parent page was not found.");
            order = parentPage.SubPages.Count;
        }

        var page = Page.Create(request.DocumentId, request.ParentPageId, request.Title, string.Empty, order);
        
        await documentRepository.AddPageAsync(page, cancellationToken);
        
        await documentRepository.SaveChangesAsync(cancellationToken);
        
        return Result<Guid>.Success(page.Id);
    }
}
