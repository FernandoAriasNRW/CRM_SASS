using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Commands;

public record SaveAsTemplateCommand(
    Guid TenantId,
    Guid OwnerId,
    Guid DocumentId,
    string? CustomTitle = null,
    string? Description = null) : IRequest<Result<Guid>>, IAuthorizeEntity
{
    public string EntityType => "Document";
    public Guid EntityId => DocumentId;
    public string RequiredPermission => "Write";
}
