using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Commands;
using Docs.Domain.Entities;
using Docs.Domain.ValueObjects;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public class SaveAsTemplateHandler(IDocumentRepository repository) 
    : IRequestHandler<SaveAsTemplateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SaveAsTemplateCommand request, CancellationToken cancellationToken)
    {
        var sourceDoc = await repository.GetByIdAsync(request.DocumentId, cancellationToken);
        if (sourceDoc == null)
            return Result<Guid>.Failure("Source document not found");

        var templateTitle = !string.IsNullOrWhiteSpace(request.CustomTitle) 
            ? request.CustomTitle 
            : $"{sourceDoc.Title} (Template)";

        var templateDesc = !string.IsNullOrWhiteSpace(request.Description)
            ? request.Description
            : sourceDoc.Description;

        // Create new Template document
        var templateDoc = Document.Create(
            request.TenantId,
            templateTitle,
            templateDesc,
            DocumentType.Template,
            request.OwnerId,
            sourceDoc.TeamId,
            sourceDoc.ProjectId);

        var permission = DocumentPermission.CreateForUser(templateDoc.Id, request.OwnerId, true, true, true);
        templateDoc.AddPermission(permission);

        await repository.AddAsync(templateDoc, cancellationToken);

        // Copy pages
        var sourcePages = await repository.GetPagesByDocumentIdAsync(sourceDoc.Id, cancellationToken);
        var pageMap = new Dictionary<Guid, Guid>();

        foreach (var srcPage in sourcePages.Where(p => !p.IsDeleted))
        {
            Guid? newParentId = srcPage.ParentPageId.HasValue && pageMap.ContainsKey(srcPage.ParentPageId.Value)
                ? pageMap[srcPage.ParentPageId.Value]
                : null;

            var newPage = Page.Create(templateDoc.Id, newParentId, srcPage.Title, srcPage.Content, srcPage.Order);
            pageMap[srcPage.Id] = newPage.Id;
            await repository.AddPageAsync(newPage, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(templateDoc.Id);
    }
}
