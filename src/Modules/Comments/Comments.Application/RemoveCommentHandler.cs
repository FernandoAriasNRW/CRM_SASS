using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public sealed class RemoveCommentHandler(
    ICommentRepository repository,
    ICommentsUnitOfWork unitOfWork) : ICommandHandler<RemoveCommentCommand, bool>
{
    public async Task<Result<bool>> Handle(RemoveCommentCommand request, CancellationToken ct)
    {
        var comment = await repository.GetByIdAsync(request.TenantId, request.Id, ct);
        if (comment is null) return Result<bool>.Failure(Comment.Rules.NotFound);

        if (!comment.CanDelete(request.DeletedBy, request.Role))
            return Result<bool>.Failure(Comment.Rules.OnlyAuthorOrAdminDeletes);

        await repository.RemoveAsync(comment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
