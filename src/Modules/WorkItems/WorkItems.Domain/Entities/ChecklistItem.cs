namespace WorkItems.Domain.Entities;

/// <summary>
/// Un punto de la checklist de una tarea.
///
/// Como los responsables, es una colección propiedad de <see cref="WorkTask"/>: el texto, el
/// orden y el estado de cada punto son cosa de la tarea, y una tabla suelta con su repositorio
/// permitiría dejar puntos huérfanos o con posiciones repetidas sin que nadie se enterara.
///
/// <see cref="Position"/> es explícita porque el orden de una checklist **lo decide quien la
/// escribe**, y una colección propiedad del agregado no vuelve ordenada de la base de datos.
/// Confiar en el orden de llegada es lo que ya nos jugó una mala pasada con los responsables.
/// </summary>
public sealed class ChecklistItem
{
    public const int MaxLength = 200;

    public Guid Id { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool IsDone { get; private set; }
    public int Position { get; private set; }

    private ChecklistItem() { }

    internal ChecklistItem(string text, int position)
    {
        Id = Guid.NewGuid();
        Text = Normalize(text);
        Position = position;
    }

    internal void Rename(string text) => Text = Normalize(text);

    internal void MarkDone(bool done) => IsDone = done;

    internal void MoveTo(int position) => Position = position;

    private static string Normalize(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();

        if (trimmed.Length == 0)
            throw new InvalidOperationException(Rules.TextRequired);

        if (trimmed.Length > MaxLength)
            throw new InvalidOperationException(Rules.TextTooLong);

        return trimmed;
    }

    public static class Rules
    {
        public const string TextRequired = "El punto de la checklist necesita un texto";
        public static readonly string TextTooLong =
            $"El punto de la checklist no puede pasar de {MaxLength} caracteres";
        public const string NotFound = "Ese punto de la checklist no existe";
    }
}
