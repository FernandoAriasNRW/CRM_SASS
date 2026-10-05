using Communication.Domain.Entities;

namespace Communication.Application.DTOs;

public sealed class ConversationDto
{
  public Guid Id { get; private set; }
  public string Name { get; private set; } = default!;
  public int TypeValue { get; private set; }

  /// <summary>
  /// El tipo por su nombre (<c>Channel</c>, <c>Group</c>, <c>Direct</c>), que es lo que enseña la
  /// interfaz. Sólo con <c>TypeValue</c> el chat leía un <c>type</c> que no llegaba nunca.
  /// </summary>
  public string Type { get; private set; } = default!;
  public DateTime CreatedAt { get; private set; }

  private ConversationDto()
  { }

  private ConversationDto(Guid id, string name, int typeValue, string type, DateTime createdAt)
  {
    Id = id;
    Name = name;
    TypeValue = typeValue;
    Type = type;
    CreatedAt = createdAt;
  }

  // Mapping desde entidad
  public static ConversationDto FromDomain(Conversation entity)
  {
    return new ConversationDto(
        entity.Id,
        entity.Name,
        entity.TypeValue,
        entity.Type.Name,
        entity.CreatedAt
    );
  }
}