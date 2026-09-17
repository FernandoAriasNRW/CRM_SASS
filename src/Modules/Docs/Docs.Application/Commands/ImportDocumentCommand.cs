using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Commands;

public record ImportDocumentCommand(
    Guid TenantId,
    Guid OwnerId,
    string Title,
    string Content,
    int Type = 1) : IRequest<Result<Guid>>, IAuthorizeEntity
{
    public string EntityType => "Document";
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";
}
