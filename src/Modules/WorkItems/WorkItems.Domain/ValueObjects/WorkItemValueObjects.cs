using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace WorkItems.Domain.ValueObjects;

public sealed class TaskTitle : ValueObject
{
    public string Value { get; init; }

    private TaskTitle() { }
    private TaskTitle(string value) => Value = value;

    public static Result<TaskTitle> Create(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result<TaskTitle>.Failure("Título es requerido");

        if (title.Length > 200)
            return Result<TaskTitle>.Failure("Título excede 200 caracteres");

        return Result<TaskTitle>.Success(new TaskTitle(title.Trim()));
    }

    public override IEnumerable<object> GetEqualityComponents() => [Value];
}

public sealed class TaskStatus : Enumeration<string>
{
    public static readonly TaskStatus ToDo = new("To Do", "Por Hacer");
    public static readonly TaskStatus InProgress = new("In Progress", "En Progreso");
    public static readonly TaskStatus InReview = new("In Review", "En Revisión");
    public static readonly TaskStatus Done = new("Done", "Completado");
    public static readonly TaskStatus OnHold = new("On Hold", "En Espera");

    private TaskStatus() : base(string.Empty, string.Empty) { }
    public TaskStatus(string value, string name) : base(value, name) { }

    public static IReadOnlyList<string> GetValidTransitions(string currentStatus) => currentStatus switch
    {
        "To Do"       => ["In Progress", "Done"],
        "In Progress" => ["To Do", "In Review", "Done", "On Hold"],
        "In Review"   => ["In Progress", "Done"],
        "Done"        => ["To Do"],
        "On Hold"     => ["To Do", "In Progress"],
        _             => []
    };
}
