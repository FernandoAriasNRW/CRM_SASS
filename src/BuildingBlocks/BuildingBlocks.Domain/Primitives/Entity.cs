namespace BuildingBlocks.Domain.Primitives;

public abstract class Entity
{
  public Guid Id { get; protected set; }

  private readonly List<IDomainEvent> _domainEvents = [];
  public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

  protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

  public void ClearDomainEvents() => _domainEvents.Clear();

  public override bool Equals(object? obj)
  {
    if (obj is not Entity other)
      return false;

    if (ReferenceEquals(this, other))
      return true;

    if (GetType() != other.GetType())
      return false;

    if (Id == Guid.Empty || other.Id == Guid.Empty)
      return false;

    return Id == other.Id;
  }

  public override int GetHashCode() => Id.GetHashCode();

  public static bool operator ==(Entity? left, Entity? right)
  {
    if (left is null && right is null)
      return true;

    if (left is null || right is null)
      return false;

    return left.Equals(right);
  }

  public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}

public interface IDomainEvent
{
  Guid EventId { get; }
  DateTime OccurredOnUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
  public Guid EventId { get; } = Guid.NewGuid();
  public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}