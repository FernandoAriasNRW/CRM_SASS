using BuildingBlocks.Domain.Primitives;

namespace Teams.Domain.ValueObjects;

public sealed record TeamRole
{
    public string Name { get; }
    
    // Some basic permission flags for customization
    public bool CanManageProjects { get; }
    public bool CanManageTasks { get; }
    public bool CanManageTickets { get; }
    public bool CanManageMembers { get; }

    private TeamRole(string name, bool canManageProjects, bool canManageTasks, bool canManageTickets, bool canManageMembers)
    {
        Name = name;
        CanManageProjects = canManageProjects;
        CanManageTasks = canManageTasks;
        CanManageTickets = canManageTickets;
        CanManageMembers = canManageMembers;
    }

    public static TeamRole Owner => new("Owner", true, true, true, true);
    public static TeamRole Member => new("Member", false, true, false, false);
    public static TeamRole Viewer => new("Viewer", false, false, false, false);

    public static TeamRole CreateCustom(string name, bool canManageProjects, bool canManageTasks, bool canManageTickets, bool canManageMembers)
    {
        return new TeamRole(name, canManageProjects, canManageTasks, canManageTickets, canManageMembers);
    }
}
