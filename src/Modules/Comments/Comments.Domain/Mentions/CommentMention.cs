namespace Comments.Domain.Mentions;

/// <summary>
/// Algo mencionado en un comentario: una persona, un equipo, un proyecto, una tarea, un ticket o
/// un documento.
///
/// Se guarda aparte del texto para poder preguntar al revés —«¿qué comentarios hablan de esta
/// tarea?»— sin leer todos los comentarios de la organización, y para avisar a quien se menciona.
/// El nombre con que se escribió se guarda también: si la tarea cambia de título, la mención sigue
/// diciendo cómo se la llamó.
/// </summary>
public sealed class CommentMention
{
    public string Type { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Label { get; private set; } = string.Empty;

    private CommentMention() { }

    public CommentMention(string type, Guid entityId, string label)
    {
        Type = type;
        EntityId = entityId;
        Label = label;
    }
}

/// <summary>
/// Qué se puede mencionar en un comentario. Los valores viajan en el texto y se guardan, así que
/// cambiar uno exige migrar los comentarios.
/// </summary>
public static class MentionTypes
{
    public const string Person = "Person";
    public const string Team = "Team";
    public const string Project = "Project";
    public const string Task = "Task";
    public const string Ticket = "Ticket";
    public const string Document = "Document";

    public static IReadOnlyList<string> All() => [Person, Team, Project, Task, Ticket, Document];

    public static bool Exists(string? type) => type is not null && All().Contains(type);
}
