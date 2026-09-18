using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public sealed record AddCommentCommand(
    Guid TenantId,
    string EntityType,
    Guid EntityId,
    Guid AuthorId,
    string Text,
    Guid? ReplyToId = null) : ICommand<CommentDto>;
