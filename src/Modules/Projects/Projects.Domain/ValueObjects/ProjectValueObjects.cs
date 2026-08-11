using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace Projects.Domain.ValueObjects;

public sealed class ProjectName : ValueObject
{
    public string Value { get; init; } = string.Empty;

    private ProjectName() { }
    private ProjectName(string value) => Value = value;

    public static Result<ProjectName> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result<ProjectName>.Failure("Nombre del proyecto es requerido");

        if (name.Length > 200)
            return Result<ProjectName>.Failure("Nombre excede 200 caracteres");

        return Result<ProjectName>.Success(new ProjectName(name.Trim()));
    }

    public override IEnumerable<object> GetEqualityComponents() => [Value];
}

public sealed class ProjectStatus : Enumeration<string>
{
    public static readonly ProjectStatus Planned = new("Planned", "Planificado");
    public static readonly ProjectStatus InProgress = new("In Progress", "En Progreso");
    public static readonly ProjectStatus OnHold = new("On Hold", "En Espera");
    public static readonly ProjectStatus Done = new("Done", "Completado");
    public static readonly ProjectStatus Cancelled = new("Cancelled", "Cancelado");

    private ProjectStatus() : base(string.Empty, string.Empty) { }
    public ProjectStatus(string value, string name) : base(value, name) { }
}
