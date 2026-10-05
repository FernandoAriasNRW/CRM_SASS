using BuildingBlocks.Domain.Primitives;
using Teams.Domain.Events;

namespace Teams.Domain.Entities;

public sealed class Team : AggregateRoot, ITenantEntity, ISoftDeletable
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private readonly List<TeamMember> _members = new();
    public IReadOnlyCollection<TeamMember> Members => _members.AsReadOnly();

    private Team() { }

    public static Team Create(DateTime nowUtc, Guid tenantId, string name, string description)
    {
        var team = new Team
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = description,
            CreatedAtUtc = nowUtc,
            IsDeleted = false
        };

        team.RaiseDomainEvent(new TeamCreatedEvent(team.Id, tenantId, name));

        return team;
    }

    public void AddMember(DateTime nowUtc, Guid userId, ValueObjects.TeamRole role)
    {
        if (IsDeleted) throw new InvalidOperationException("Team is deleted.");
        
        var existingMember = _members.FirstOrDefault(m => m.UserId == userId && !m.IsDeleted);
        if (existingMember != null) return;

        var member = TeamMember.Create(nowUtc, Id, userId, role);
        _members.Add(member);
    }

    public void Update(string name, string description)
    {
        if (IsDeleted) throw new InvalidOperationException("Team is deleted.");
        Name = name;
        Description = description;
    }

    public void Delete(DateTime nowUtc)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = nowUtc;
    }
}
