using Communication.Domain.Entities;

namespace Communication.Application.DTOs;

/*public sealed record MessageDto(
    Guid Id,
    Guid TenantId,
    Guid ConversationId,
    Guid SenderId,
    string Content,
    DateTime SentAt,
    DateTime? EditedAt,
    bool IsDeleted
);*/

public sealed class MessageDto
{
  public Guid Id { get; private set; }
  public Guid TenantId { get; private set; }
  public Guid ConversationId { get; private set; }
  public Guid SenderId { get; private set; }
  public string Content { get; private set; } = string.Empty;
  public DateTime SentAt { get; private set; }
  public DateTime? EditedAt { get; private set; }
  public bool IsDeleted { get; private set; }

  private MessageDto()
  { }

  private MessageDto(Guid tenantId, Guid conversationId, Guid senderId, string content, DateTime sentAt, DateTime? editedAt, bool isDeleted)
  {
    TenantId = tenantId;
    ConversationId = conversationId;
    SenderId = senderId;
    Content = content;
    SentAt = sentAt;
    EditedAt = editedAt;
    IsDeleted = isDeleted;
  }

  public MessageDto(Guid id, Guid tenantId, Guid conversationId, Guid senderId, string content, DateTime sentAt)
  {
    Id = id;
    TenantId = tenantId;
    ConversationId = conversationId;
    SenderId = senderId;
    Content = content;
    SentAt = sentAt;
  }

  // Mapping desde entidad
  public static MessageDto FromDomain(Message entity)
  {
    return new MessageDto(
        entity.TenantId,
        entity.ConversationId,
        entity.SenderId,
        entity.Content,
        entity.SentAt,
        entity.EditedAt,
        entity.IsDeleted
    );
  }
}