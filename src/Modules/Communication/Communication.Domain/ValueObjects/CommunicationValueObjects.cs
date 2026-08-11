using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Communication.Domain.ValueObjects;

public sealed class MessageContent : ValueObject
{
  public string Value { get; }

  private MessageContent() { }
  private MessageContent(string value) => Value = value;

  public static Result<MessageContent> Create(string content)
  {
    if (string.IsNullOrWhiteSpace(content))
      return Result<MessageContent>.Failure("Content is required");

    if (content.Length > 10000)
      return Result<MessageContent>.Failure("Content must not exceed 10000 characters");

    return Result<MessageContent>.Success(new MessageContent(content));
  }

  public override IEnumerable<object> GetEqualityComponents()
  {
    yield return Value;
  }
}

public sealed class ConversationType : Enumeration
{
  public static readonly ConversationType Direct = new(1, "Direct");
  public static readonly ConversationType Group = new(2, "Group");
  public static readonly ConversationType Channel = new(3, "Channel");

  private ConversationType() : base(0, string.Empty) { }
  private ConversationType(int value, string name) : base(value, name)
  {
  }

  public static IReadOnlyList<ConversationType> All() => GetAll<ConversationType>();
}