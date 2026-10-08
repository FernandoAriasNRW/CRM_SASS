using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Domain.Entities;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public record CreatePageCommand(Guid DocumentId, Guid? ParentPageId, string Title) : IRequest<Result<Guid>>, IAuthorizeEntity
{
    public string EntityType => "Document";
    public Guid EntityId => DocumentId;
    public string RequiredPermission => "Write";
}

public class CreatePageHandler(TimeProvider timeProvider, IDocumentRepository documentRepository) : IRequestHandler<CreatePageCommand, Result<Guid>>
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
            // La página madre tiene que ser de este mismo documento: si no, la nueva colgaría de
            // un árbol ajeno y no aparecería en ninguno de los dos.
            if (parentPage == null || parentPage.DocumentId != document.Id)
                return Result<Guid>.Failure("The parent page was not found.");
            order = parentPage.SubPages.Count;
        }

        var page = Page.Create(timeProvider.GetUtcNow().UtcDateTime, document.TenantId, document.Id, request.ParentPageId, request.Title, string.Empty, order);
        
        await documentRepository.AddPageAsync(page, cancellationToken);
        
        await documentRepository.SaveChangesAsync(cancellationToken);
        
        return Result<Guid>.Success(page.Id);
    }
}
