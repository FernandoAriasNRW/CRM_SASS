using BuildingBlocks.Domain.Primitives;
using BuildingBlocks.Infrastructure.DomainEvents;
using BuildingBlocks.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

public class UnitOfWork<TContext>(
    TContext context,
    IOutboxService outboxService,
    IDomainEventDispatcher? domainEventDispatcher = null) : IUnitOfWork<TContext>
    where TContext : DbContext
{
  private readonly TContext _context = context;
  private readonly IOutboxService _outboxService = outboxService;
  private readonly IDomainEventDispatcher? _domainEventDispatcher = domainEventDispatcher;

  public async Task<int> SaveChangesAsync(CancellationToken ct = default)
  {
    // 1. Save entity changes first
    var result = await _context.SaveChangesAsync(ct);

    // 2. Collect domain events from all tracked entities
    var domainEvents = CollectDomainEvents();

    // 3. If we have events and an outbox service, persist them
    if (domainEvents.Count > 0)
    {
      foreach (var @event in domainEvents)
      {
        var eventType = @event.GetType().FullName ?? @event.GetType().Name;
        var payload = System.Text.Json.JsonSerializer.Serialize(@event, @event.GetType());
        await _outboxService.AddMessageAsync(eventType, payload, ct);
      }
    }

    return result;
  }

  public async Task<int> SaveChangesAndDispatchAsync(CancellationToken ct = default)
  {
    // 1. Save entity changes first
    var result = await _context.SaveChangesAsync(ct);

    // 2. Collect domain events from all tracked entities
    var domainEvents = CollectDomainEvents();

    // 3. If we have events, persist to outbox and dispatch via MediatR
    if (domainEvents.Count > 0)
    {
      foreach (var @event in domainEvents)
      {
        var eventType = @event.GetType().FullName ?? @event.GetType().Name;
        var payload = System.Text.Json.JsonSerializer.Serialize(@event, @event.GetType());
        await _outboxService.AddMessageAsync(eventType, payload, ct);
      }

      // 4. Dispatch in-process via MediatR if dispatcher is available
      if (_domainEventDispatcher != null)
      {
        await _domainEventDispatcher.DispatchAsync(domainEvents, ct);
      }
    }

    return result;
  }

  public async Task BeginTransactionAsync(CancellationToken ct = default)
      => await _context.Database.BeginTransactionAsync(ct);

  public async Task CommitTransactionAsync(CancellationToken ct = default)
  {
    await _context.Database.CommitTransactionAsync(ct);
  }

  public async Task RollbackTransactionAsync(CancellationToken ct = default)
      => await _context.Database.RollbackTransactionAsync(ct);

  /// <summary>
  /// Collects domain events from all tracked AggregateRoot entities.
  /// </summary>
  private IReadOnlyCollection<IDomainEvent> CollectDomainEvents()
  {
    var domainEvents = new List<IDomainEvent>();

    var entities = _context.ChangeTracker
        .Entries<AggregateRoot>()
        .Where(e => e.Entity.DomainEvents.Count > 0)
        .ToList();

    foreach (var entry in entities)
    {
      var events = entry.Entity.DomainEvents.ToList();
      domainEvents.AddRange(events);
      entry.Entity.ClearDomainEvents();
    }

    return domainEvents;
  }
}