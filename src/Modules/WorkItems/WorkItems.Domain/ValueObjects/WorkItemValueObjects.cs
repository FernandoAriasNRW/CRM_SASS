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

/// <summary>
/// Prioridad de una tarea.
///
/// Sigue el patrón de <see cref="TaskStatus"/>, con una diferencia que importa: la
/// prioridad **tiene orden**. «Urgent» va antes que «Low», y ese orden es de negocio, no
/// alfabético —alfabéticamente saldría High, Low, Normal, Urgent, que no significa nada—.
/// Ese orden vive en <see cref="Orden"/> y es lo que deben usar las listas y los tableros.
///
/// Como en el estado, no hay reglas sobre qué cambio de prioridad es válido: subir o bajar
/// una tarea es decisión de quien gestiona el trabajo. Sólo se rechaza una prioridad que no
/// exista, que sería un dato corrupto.
/// </summary>
public sealed class TaskPriority : Enumeration<string>
{
    public static readonly TaskPriority Urgent = new("Urgent", "Urgente");
    public static readonly TaskPriority High = new("High", "Alta");
    public static readonly TaskPriority Normal = new("Normal", "Normal");
    public static readonly TaskPriority Low = new("Low", "Baja");

    /// <summary>
    /// La que recibe una tarea que no declara prioridad, y con la que se rellenan las
    /// tareas que ya existían antes de que hubiera prioridades.
    /// </summary>
    public static readonly TaskPriority PorDefecto = Normal;

    private TaskPriority() : base(string.Empty, string.Empty) { }
    public TaskPriority(string value, string name) : base(value, name) { }

    /// <summary>De más urgente a menos. El orden de presentación por defecto.</summary>
    public static IReadOnlyList<TaskPriority> All() =>
        [Urgent, High, Normal, Low];

    public static bool Existe(string priority) =>
        All().Any(p => p.Value == priority);

    /// <summary>
    /// Devuelve la instancia canónica, con su nombre en español ya puesto.
    ///
    /// Existe para no construir la prioridad a mano desde su valor, que es lo que hace
    /// <see cref="TaskStatus"/> al mover una tarea y deja el nombre igual que el valor —una
    /// tarea movida a «Done» acaba con nombre «Done» en lugar de «Completado»—.
    /// </summary>
    public static TaskPriority Desde(string priority) =>
        All().FirstOrDefault(p => p.Value == priority)
        ?? throw new InvalidOperationException($"La prioridad '{priority}' no existe");

    /// <summary>
    /// Posición en el orden de negocio: 0 es lo más urgente.
    ///
    /// Se expone como función estática sobre la cadena, y no sólo como propiedad de la
    /// instancia, porque las consultas necesitan ordenar por prioridad en la base de datos
    /// —donde sólo hay una columna de texto— y hacerlo por esa columna daría el orden
    /// alfabético, que es incorrecto.
    /// </summary>
    public static int OrdenDe(string priority)
    {
        var indice = All().ToList().FindIndex(p => p.Value == priority);
        return indice >= 0 ? indice : All().Count;
    }

    public int Orden => OrdenDe(Value);
}
