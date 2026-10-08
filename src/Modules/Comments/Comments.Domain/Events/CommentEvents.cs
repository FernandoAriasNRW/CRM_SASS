using BuildingBlocks.Domain.Primitives;

namespace Comments.Domain.Events;

public sealed record CommentAddedEvent(
    Guid CommentId, Guid TenantId, string EntityType, Guid EntityId, Guid AuthorId) : DomainEvent;

public sealed record CommentEditedEvent(Guid CommentId, Guid TenantId, Guid AuthorId) : DomainEvent;

public sealed record CommentRemovedEvent(Guid CommentId, Guid TenantId, Guid DeletedBy) : DomainEvent;

/// <summary>Algo que un comentario menciona: su tipo —ver <c>MentionTypes</c>— y su identificador.</summary>
public sealed record MentionedEntity(string Type, Guid EntityId);

/// <summary>
/// Un comentario menciona cosas que antes no mencionaba: al crearse, todas las que lleve; al
/// editarse, sólo las que se añadieron. Es lo que hace falta para avisar a quien se menciona.
/// </summary>
public sealed record CommentMentionsAddedEvent(
    Guid CommentId, Guid TenantId, string EntityType, Guid EntityId, Guid AuthorId,
    IReadOnlyList<MentionedEntity> Mentions) : DomainEvent;
