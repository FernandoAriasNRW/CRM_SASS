using BuildingBlocks.Domain;
using BuildingBlocks.Domain.Primitives;

namespace WorkItems.Domain.ValueObjects;

public sealed class TaskTitle : ValueObject
{
    public string Value { get; init; }

    private TaskTitle() { Value = null!; } // EF las rellena al materializar.
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

    public static IReadOnlyList<TaskStatus> All() =>
        [ToDo, InProgress, InReview, Done, OnHold];

    /// <summary>
    /// Indica si el estado existe.
    ///
    /// No hay reglas sobre qué transición es válida: una tarea puede pasar de cualquier
    /// estado a cualquier otro. Decidir si un movimiento tiene sentido es del equipo que
    /// gestiona el trabajo, no del sistema, y una máquina de estados rígida acaba
    /// estorbando en los casos reales —reabrir algo dado por hecho, mandar a espera algo
    /// que ni se empezó— sin evitar ningún dato incorrecto.
    ///
    /// Lo que sí se comprueba es que el estado exista: mover a uno inventado corrompería
    /// los datos, y eso no es política de flujo sino integridad.
    /// </summary>
    public static bool Existe(string status) =>
        All().Any(s => s.Value == status);
}
