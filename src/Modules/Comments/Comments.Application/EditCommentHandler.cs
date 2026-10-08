using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public sealed class EditCommentHandler(
    TimeProvider timeProvider,
    ICommentRepository repository,
    ICommentsUnitOfWork unitOfWork) : ICommandHandler<EditCommentCommand, bool>
{
    public async Task<Result<bool>> Handle(EditCommentCommand request, CancellationToken ct)
    {
        var comment = await repository.GetByIdAsync(request.TenantId, request.Id, ct);
        if (comment is null) return Result<bool>.Failure(Comment.Rules.NotFound);

        try { comment.Edit(timeProvider.GetUtcNow().UtcDateTime, request.EditedBy, request.Text); }
        catch (InvalidOperationException ex) { return Result<bool>.Failure(ex.Message); }

        await repository.UpdateAsync(comment, ct);
        await unitOfWork.SaveChangesAndDispatchAsync(ct);

        return Result<bool>.Success(true);
    }
}
