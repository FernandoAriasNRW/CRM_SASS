using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public static class CommentMapping
{
    public static CommentDto ToDto(Comment c) =>
        new(c.Id, c.AuthorId, c.Text, c.CreatedAtUtc, c.EditedAtUtc, c.ReplyToId);
}
