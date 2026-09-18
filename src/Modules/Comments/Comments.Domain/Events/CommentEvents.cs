using BuildingBlocks.Domain.Primitives;

namespace Comments.Domain.Events;

public sealed record CommentAddedEvent(
    Guid CommentId, Guid TenantId, string EntityType, Guid EntityId, Guid AuthorId) : DomainEvent;

public sealed record CommentEditedEvent(Guid CommentId, Guid TenantId, Guid AuthorId) : DomainEvent;

public sealed record CommentRemovedEvent(Guid CommentId, Guid TenantId, Guid DeletedBy) : DomainEvent;
