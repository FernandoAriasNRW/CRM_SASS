using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Communication.Domain.Events;
using Communication.Domain.ValueObjects;

namespace Communication.Domain.Entities;

/// <summary>
/// Entidad de dominio Conversation.
/// </summary>
public sealed class Conversation : AggregateRoot, ITenantEntity, ISoftDeletable
{
  public Guid TenantId { get; private set; }
  public string Name { get; private set; } = string.Empty;
  public int TypeValue { get; private set; }
  public DateTime CreatedAt { get; private set; }
  public DateTime? LastMessageAt { get; private set; }

  // Soft Delete
  public bool IsDeleted { get; private set; }

  public DateTime? DeletedAt { get; private set; }
  public Guid? DeletedBy { get; private set; }

  public ConversationType Type => ConversationType.FromValue<ConversationType>(TypeValue);

  private Conversation()
  { }

  public static Result<Conversation> Create(Guid tenantId, string name, ConversationType type)
  {
    if (string.IsNullOrWhiteSpace(name))
      return Result<Conversation>.Failure("El nombre de la conversación es requerido");

    if (name.Length > 200)
      return Result<Conversation>.Failure("El nombre no puede exceder 200 caracteres");

    var conversation = new Conversation
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      Name = name,
      TypeValue = type.Value,
      CreatedAt = DateTime.UtcNow,
      IsDeleted = false,
      DeletedAt = null,
      DeletedBy = null
    };

    conversation.RaiseDomainEvent(new ConversationCreatedEvent(conversation.Id, tenantId, name));
    return Result<Conversation>.Success(conversation);
  }

  public void UpdateName(string newName)
  {
    if (IsDeleted)
      throw new InvalidOperationException("No se puede modificar una conversación eliminada");

    if (string.IsNullOrWhiteSpace(newName))
      throw new ArgumentException("El nombre no puede estar vacío");

    Name = newName;
  }

  /// <summary>
  /// Agrega un mensaje a la conversación.
  /// </summary>
  public void AddMessage(Message message)
  {
    if (IsDeleted)
      throw new InvalidOperationException("No se puede agregar mensajes a una conversación eliminada");

    LastMessageAt = DateTime.UtcNow;
    RaiseDomainEvent(new MessageAddedEvent(message.Id, Id, TenantId));
  }

  /// <summary>
  /// Soft delete de la conversación.
  /// </summary>
  public void Delete(Guid deletedBy)
  {
    if (IsDeleted)
      throw new InvalidOperationException("La conversación ya ha sido eliminada");

    IsDeleted = true;
    DeletedAt = DateTime.UtcNow;
    DeletedBy = deletedBy;

    RaiseDomainEvent(new ConversationDeletedEvent(Id, TenantId, deletedBy));
  }

  /// <summary>
  /// Restaura una conversación eliminada.
  /// </summary>
  public void Restore()
  {
    if (!IsDeleted)
      throw new InvalidOperationException("La conversación no está eliminada");

    IsDeleted = false;
    DeletedAt = null;
    DeletedBy = null;
  }
}

/// <summary>
/// Entidad de dominio Message.
/// </summary>
public sealed class Message : AggregateRoot, ITenantEntity, ISoftDeletable
{
  public Guid TenantId { get; private set; }
  public Guid ConversationId { get; set; }
  public Guid SenderId { get; set; }
  public string Content { get; set; } = string.Empty;
  public DateTime SentAt { get; set; }
  public DateTime? EditedAt { get; set; }

  // Soft Delete
  public bool IsDeleted { get; set; }

  public DateTime? DeletedAt { get; private set; }
  public Guid? DeletedBy { get; private set; }

  private Message()
  { }

  public static Result<Message> Create(Guid tenantId, Guid conversationId, Guid senderId, string content)
  {
    var contentResult = MessageContent.Create(content);
    if (contentResult.IsFailure)
      return Result<Message>.Failure(contentResult.Error!);

    var message = new Message
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      ConversationId = conversationId,
      SenderId = senderId,
      Content = content,
      SentAt = DateTime.UtcNow,
      IsDeleted = false,
      DeletedAt = null,
      DeletedBy = null
    };

    return Result<Message>.Success(message);
  }

  public Result<Message> Edit(string newContent, Guid editorId)
  {
    if (IsDeleted)
      return Result<Message>.Failure("No se puede editar un mensaje eliminado");

    // Solo el remitente puede editar
    if (SenderId != editorId)
      return Result<Message>.Failure("Solo el remitente puede editar este mensaje");

    var contentResult = MessageContent.Create(newContent);
    if (contentResult.IsFailure)
      return Result<Message>.Failure(contentResult.Error!);

    Content = newContent;
    EditedAt = DateTime.UtcNow;

    return Result<Message>.Success(this);
  }

  /// <summary>
  /// Soft delete del mensaje.
  /// </summary>
  public void Delete(Guid deletedBy)
  {
    if (IsDeleted)
      throw new InvalidOperationException("El mensaje ya ha sido eliminado");

    IsDeleted = true;
    DeletedAt = DateTime.UtcNow;
    DeletedBy = deletedBy;

    RaiseDomainEvent(new MessageDeletedEvent(Id, TenantId, deletedBy));
  }

  /// <summary>
  /// Restaura un mensaje eliminado.
  /// </summary>
  public void Restore()
  {
    if (!IsDeleted)
      throw new InvalidOperationException("El mensaje no está eliminado");

    IsDeleted = false;
    DeletedAt = null;
    DeletedBy = null;
  }
}