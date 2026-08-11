using Communication.Domain.Entities;

namespace Communication.Application.DTOs;

public sealed class ConversationDto
{
  public Guid Id { get; private set; }
  public string Name { get; private set; } = default!;
  public int TypeValue { get; private set; }
  public DateTime CreatedAt { get; private set; }

  private ConversationDto()
  { }

  private ConversationDto(Guid id, string name, int type, DateTime createdAt)
  {
    Id = id;
    Name = name;
    TypeValue = type;
    CreatedAt = createdAt;
  }

  // Mapping desde entidad
  public static ConversationDto FromDomain(Conversation entity)
  {
    return new ConversationDto(
        entity.Id,
        entity.Name,
        entity.TypeValue,
        entity.CreatedAt
    );
  }
}