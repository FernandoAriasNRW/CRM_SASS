using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Notifications.Domain.ValueObjects;

public sealed class NotificationContent : ValueObject
{
  public string Subject { get; }
  public string Body { get; }

  private NotificationContent() { }
  private NotificationContent(string subject, string body)
  {
    Subject = subject;
    Body = body;
  }

  public static Result<NotificationContent> Create(string subject, string body)
  {
    if (string.IsNullOrWhiteSpace(subject))
      return Result<NotificationContent>.Failure("Subject is required");

    if (subject.Length > 200)
      return Result<NotificationContent>.Failure("Subject must not exceed 200 characters");

    if (string.IsNullOrWhiteSpace(body))
      return Result<NotificationContent>.Failure("Body is required");

    return Result<NotificationContent>.Success(new NotificationContent(subject, body));
  }

  public override IEnumerable<object> GetEqualityComponents()
  {
    yield return Subject;
    yield return Body;
  }
}

public sealed class NotificationType : Enumeration
{
  public static readonly NotificationType Email = new(1, "Email");
  public static readonly NotificationType Sms = new(2, "Sms");
  public static readonly NotificationType Push = new(3, "Push");
  public static readonly NotificationType InApp = new(4, "InApp");

  private NotificationType() : base(0, string.Empty) { }
  private NotificationType(int value, string name) : base(value, name)
  {
  }

  public static IReadOnlyList<NotificationType> All() => GetAll<NotificationType>();
}

public sealed class NotificationStatus : Enumeration
{
  public static readonly NotificationStatus Pending = new(1, "Pending");
  public static readonly NotificationStatus Sent = new(2, "Sent");
  public static readonly NotificationStatus Failed = new(3, "Failed");
  public static readonly NotificationStatus Read = new(4, "Read");

  private NotificationStatus() : base(0, string.Empty) { }
  private NotificationStatus(int value, string name) : base(value, name)
  {
  }

  public static IReadOnlyList<NotificationStatus> All() => GetAll<NotificationStatus>();
}