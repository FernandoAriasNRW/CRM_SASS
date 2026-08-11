using BuildingBlocks.Domain;
using MediatR;

namespace Docs.Application.Queries;

public record PageDto(Guid Id, Guid DocumentId, Guid? ParentPageId, string Title, string Content, int Order);

public record GetPagesQuery(Guid DocumentId) : IRequest<Result<List<PageDto>>>;
