using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public sealed class AddCommentHandler(
    ICommentRepository repository,
    ICommentsUnitOfWork unitOfWork) : ICommandHandler<AddCommentCommand, CommentDto>
{
    public async Task<Result<CommentDto>> Handle(AddCommentCommand request, CancellationToken ct)
    {
        // Las dos reglas del anidamiento hablan de otra fila, así que no las puede comprobar el
        // agregado: que a lo que se responde exista y no sea ya una respuesta, y que esté en el
        // mismo hilo. Sin la segunda, una respuesta podría colgarse de otra entidad y aparecer
        // en un hilo donde nadie la escribió.
        if (request.ReplyToId.HasValue)
        {
            var parent = await repository.GetByIdAsync(request.TenantId, request.ReplyToId.Value, ct);

            if (parent is null)
                return Result<CommentDto>.Failure(Comment.Rules.NotFound);

            if (parent.ReplyToId.HasValue)
                return Result<CommentDto>.Failure(Comment.Rules.ReplyToReply);

            if (parent.EntityType != request.EntityType || parent.EntityId != request.EntityId)
                return Result<CommentDto>.Failure(Comment.Rules.ReplyToOtherEntity);
        }

        Comment comment;
        try
        {
            comment = Comment.Create(
                request.TenantId, request.EntityType, request.EntityId,
                request.AuthorId, request.Text, request.ReplyToId);
        }
        catch (InvalidOperationException ex) { return Result<CommentDto>.Failure(ex.Message); }

        await repository.AddAsync(comment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<CommentDto>.Success(CommentMapping.ToDto(comment));
    }
}
