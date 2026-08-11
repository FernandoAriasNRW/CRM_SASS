namespace Tags.Domain.ValueObjects;

public class TagCategory
{
    public const string Team = "Team";
    public const string Project = "Project";
    public const string Feature = "Feature";
    public const string Bug = "Bug";
    public const string Fix = "Fix";
    public const string Requirement = "Requirement";
    public const string Implementation = "Implementation";
    public const string General = "General";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Team, Project, Feature, Bug, Fix, Requirement, Implementation, General
    };

    public static bool IsValid(string category) => All.Contains(category);
}
