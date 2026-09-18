using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public sealed class GetCommentsHandler(ICommentRepository repository)
    : IQueryHandler<GetCommentsQuery, IReadOnlyList<CommentDto>>
{
    public async Task<Result<IReadOnlyList<CommentDto>>> Handle(GetCommentsQuery request, CancellationToken ct)
    {
        if (!CommentableEntityTypes.Exists(request.EntityType))
            return Result<IReadOnlyList<CommentDto>>.Failure(Comment.Rules.UnknownEntity);

        var thread = await repository.GetThreadAsync(request.TenantId, request.EntityType, request.EntityId, ct);

        return Result<IReadOnlyList<CommentDto>>.Success(
            thread.Select(CommentMapping.ToDto).ToList());
    }
}
