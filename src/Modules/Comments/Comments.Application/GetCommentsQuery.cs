using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public sealed record GetCommentsQuery(
    Guid TenantId, string EntityType, Guid EntityId) : IQuery<IReadOnlyList<CommentDto>>;
