using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Docs.Application.Abstractions.Repositories;
using Docs.Application.Commands;
using Docs.Domain.Entities;
using Docs.Domain.ValueObjects;
using MediatR;

namespace Docs.Application.Handlers.Commands;

public class CreateDocumentHandler(IDocumentRepository repository) 
    : IRequestHandler<CreateDocumentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = Document.Create(
            request.TenantId,
            request.Title,
            request.Description,
            (DocumentType)request.Type,
            request.OwnerId,
            request.TeamId,
            request.ProjectId);

        // Assign basic permissions
        var permission = DocumentPermission.CreateForUser(document.Id, request.OwnerId, true, true, true);
        document.AddPermission(permission);

        // If it's a team document, add team permission
        if (request.TeamId.HasValue)
        {
            var teamPerm = DocumentPermission.CreateForTeam(document.Id, request.TeamId.Value, true, true, true);
            document.AddPermission(teamPerm);
        }

        await repository.AddAsync(document, cancellationToken);

        var initialContent = string.IsNullOrWhiteSpace(request.InitialContent) 
            ? "<p>Start typing or use / for commands...</p>" 
            : request.InitialContent;
            
        var page = Page.Create(document.Id, null, request.Title, initialContent, 0);
        await repository.AddPageAsync(page, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(document.Id);
    }
}
