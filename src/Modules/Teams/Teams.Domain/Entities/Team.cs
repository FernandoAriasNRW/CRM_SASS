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

    /// <summary>Quiénes están en el equipo ahora: los miembros dados de baja no cuentan.</summary>
    public IReadOnlyList<Guid> ActiveMemberIds =>
        _members.Where(m => !m.IsDeleted).Select(m => m.UserId).ToList();

    /// <summary>
    /// Deja como miembros exactamente a estas personas: da de baja a quien ya no está y añade a
    /// quien falta. Quien sigue conserva su fila, con su rol y su fecha de entrada.
    /// </summary>
    public void SetMembers(DateTime nowUtc, IEnumerable<Guid> userIds)
    {
        if (IsDeleted) throw new InvalidOperationException("Team is deleted.");

        var wanted = userIds.Where(id => id != Guid.Empty).ToHashSet();
        var before = ActiveMemberIds.ToHashSet();

        foreach (var member in _members.Where(m => !m.IsDeleted && !wanted.Contains(m.UserId)))
            member.Remove(nowUtc);

        foreach (var userId in wanted)
            AddMember(nowUtc, userId, ValueObjects.TeamRole.Member);

        // Quién entra y quién sale, para avisarles. Sólo si hay cambios: guardar el mismo equipo no
        // es una novedad para nadie.
        var added = wanted.Where(id => !before.Contains(id)).ToList();
        var removed = before.Where(id => !wanted.Contains(id)).ToList();
        if (added.Count > 0 || removed.Count > 0)
            RaiseDomainEvent(new TeamMembersChangedEvent(Id, TenantId, Name, added, removed));
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
