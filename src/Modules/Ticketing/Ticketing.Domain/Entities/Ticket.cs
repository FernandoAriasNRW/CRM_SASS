using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;
using Ticketing.Domain.ValueObjects;
using Ticketing.Domain.Events;

namespace Ticketing.Domain.Entities;

public sealed class Ticket : AggregateRoot, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? AssignedAgentId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int PriorityValue { get; private set; }
    public int StatusValue { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public List<Guid> TagIds { get; private set; } = new();

    public TicketPriority Priority => TicketPriority.FromValue<TicketPriority>(PriorityValue);
    public TicketStatus Status => TicketStatus.FromValue<TicketStatus>(StatusValue);

    private Ticket() { }

    public static Result<Ticket> Create(
        Guid tenantId,
        Guid customerId,
        string title,
        string description,
        TicketPriority priority)
    {
        var titleResult = TicketTitle.Create(title);
        if (titleResult.IsFailure)
            return Result<Ticket>.Failure(titleResult.Error!);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerId = customerId,
            Title = title,
            Description = description,
            PriorityValue = priority.Value,
            StatusValue = TicketStatus.Open.Value,
            CreatedAt = DateTime.UtcNow
        };

        ticket.RaiseDomainEvent(new TicketCreatedEvent(ticket.Id, tenantId));
        return Result<Ticket>.Success(ticket);
    }

    public bool ChangeStatus(TicketStatus newStatus)
    {
        if (!Status.CanTransitionTo(newStatus))
            return false;

        var previousStatus = StatusValue;
        StatusValue = newStatus.Value;

        if (newStatus == TicketStatus.Resolved)
            ResolvedAt = DateTime.UtcNow;

        RaiseDomainEvent(new TicketStatusChangedEvent(Id, TenantId, previousStatus, newStatus.Value));
        return true;
    }

    public void AssignTo(Guid agentId)
    {
        AssignedAgentId = agentId;
        RaiseDomainEvent(new TicketAssignedEvent(Id, TenantId, agentId));
    }

    public void Unassign()
    {
        AssignedAgentId = null;
        RaiseDomainEvent(new TicketUnassignedEvent(Id, TenantId));
    }

    public void AddTag(Guid tagId)
    {
        if (!TagIds.Contains(tagId))
        {
            TagIds.Add(tagId);
        }
    }

    public void RemoveTag(Guid tagId)
    {
        if (TagIds.Contains(tagId))
        {
            TagIds.Remove(tagId);
        }
    }
}
