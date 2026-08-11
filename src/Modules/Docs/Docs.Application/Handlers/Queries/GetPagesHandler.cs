using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Queries;
using MediatR;

namespace Docs.Application.Handlers.Queries;

public class GetPagesHandler(IDocumentRepository repository) : IRequestHandler<GetPagesQuery, Result<List<PageDto>>>
{
    public async Task<Result<List<PageDto>>> Handle(GetPagesQuery request, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document == null)
            return Result<List<PageDto>>.Failure("The document was not found.");

        var pages = document.Pages
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Order)
            .Select(p => new PageDto(p.Id, p.DocumentId, p.ParentPageId, p.Title, p.Content, p.Order))
            .ToList();

        return Result<List<PageDto>>.Success(pages);
    }
}
