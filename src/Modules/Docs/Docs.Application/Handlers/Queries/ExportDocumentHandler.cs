using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using MediatR;
using System.Text;

namespace Docs.Application.Queries;

public record ExportDocumentQuery(Guid DocumentId) : IRequest<Result<string>>;

public class ExportDocumentHandler(IDocumentRepository repository) : IRequestHandler<ExportDocumentQuery, Result<string>>
{
    public async Task<Result<string>> Handle(ExportDocumentQuery request, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (document == null)
            return Result<string>.Failure("The document was not found.");

        var sb = new StringBuilder();
        sb.AppendLine($"<html><head><title>{document.Title}</title></head><body>");
        sb.AppendLine($"<h1>{document.Title}</h1>");

        var pages = document.Pages.Where(p => !p.IsDeleted).OrderBy(p => p.Order).ToList();
        
        foreach (var page in pages)
        {
            sb.AppendLine($"<h2>{page.Title}</h2>");
            // Tiptap content is usually HTML. If it's JSON, we would need a server-side renderer.
            // Assuming the frontend saves HTML or we just dump the content.
            sb.AppendLine(page.Content);
            sb.AppendLine("<hr/>");
        }
        
        sb.AppendLine("</body></html>");

        return Result<string>.Success(sb.ToString());
    }
}
