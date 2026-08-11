using BuildingBlocks.Domain.Primitives;

namespace Teams.Domain.Entities;

public sealed class TeamMember : Entity, ISoftDeletable
{
    public Guid TeamId { get; private set; }
    public Guid UserId { get; private set; }
    public ValueObjects.TeamRole Role { get; private set; } = null!;
    public DateTime JoinedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private TeamMember() { }

    internal static TeamMember Create(Guid teamId, Guid userId, ValueObjects.TeamRole role)
    {
        return new TeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = userId,
            Role = role,
            JoinedAtUtc = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    public void ChangeRole(ValueObjects.TeamRole newRole)
    {
        if (IsDeleted) throw new InvalidOperationException("Member is deleted.");
        Role = newRole;
    }

    public void Remove()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
