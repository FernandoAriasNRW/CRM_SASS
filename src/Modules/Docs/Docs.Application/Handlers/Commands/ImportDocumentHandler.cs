using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Commands;
using Docs.Domain.Entities;
using Docs.Domain.ValueObjects;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public class ImportDocumentHandler(IDocumentRepository repository) 
    : IRequestHandler<ImportDocumentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ImportDocumentCommand request, CancellationToken cancellationToken)
    {
        var title = string.IsNullOrWhiteSpace(request.Title) ? "Imported Document" : request.Title;
        var document = Document.Create(
            request.TenantId,
            title,
            "Imported document",
            (DocumentType)request.Type,
            request.OwnerId,
            null,
            null);

        var permission = DocumentPermission.CreateForUser(document.Id, request.OwnerId, true, true, true);
        document.AddPermission(permission);

        await repository.AddAsync(document, cancellationToken);

        // Convert basic plain text or markdown to simple HTML if necessary
        var content = request.Content;
        if (!content.TrimStart().StartsWith("<"))
        {
            // Simple plain text/markdown paragraph wrapping
            var paragraphs = content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            content = string.Join("", paragraphs.Select(p => $"<p>{System.Net.WebUtility.HtmlEncode(p).Replace("\n", "<br/>")}</p>"));
        }

        var page = Page.Create(document.Id, null, title, content, 0);
        await repository.AddPageAsync(page, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(document.Id);
    }
}
