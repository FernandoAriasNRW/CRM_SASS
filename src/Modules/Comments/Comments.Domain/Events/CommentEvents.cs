using BuildingBlocks.Domain.Primitives;

namespace Comments.Domain.Events;

public sealed record CommentAddedEvent(
    Guid CommentId, Guid TenantId, string EntidadDestino, Guid EntityId, Guid AutorId) : DomainEvent;

public sealed record CommentEditedEvent(Guid CommentId, Guid TenantId, Guid AutorId) : DomainEvent;

public sealed record CommentRemovedEvent(Guid CommentId, Guid TenantId, Guid QuienBorro) : DomainEvent;
