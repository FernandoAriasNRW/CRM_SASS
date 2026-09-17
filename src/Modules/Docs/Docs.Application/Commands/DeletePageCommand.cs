using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Commands;

public record DeletePageCommand(Guid PageId) : IRequest<Result>, IAuthorizeEntity
{
    // La página no lleva el documento, así que se comprueba el nivel sobre los documentos en general.
    public string EntityType => "Document";
    public Guid EntityId => Guid.Empty;
    public string RequiredPermission => "Write";
}
