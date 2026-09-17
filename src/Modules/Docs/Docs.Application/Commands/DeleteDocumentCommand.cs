using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Commands;

public record DeleteDocumentCommand(Guid DocumentId) : IRequest<Result>, IAuthorizeEntity
{
    public string EntityType => "Document";
    public Guid EntityId => DocumentId;
    public string RequiredPermission => "Write";
}
