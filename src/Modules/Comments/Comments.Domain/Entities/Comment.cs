using BuildingBlocks.Domain.Primitives;
using Comments.Domain.Events;

namespace Comments.Domain.Entities;

/// <summary>
/// Un comentario sobre una tarea, un ticket o un proyecto.
///
/// **Un solo módulo para los tres** y no uno por entidad: comentar es la misma operación en
/// todos, con las mismas reglas, y triplicarla daría tres sitios donde arreglar el mismo fallo.
/// Es la misma decisión que en campos personalizados, que ya se resuelven así.
///
/// **Se guarda quién escribió y cuándo, y no se borra el rastro al editar.** Un comentario que
/// cambia sin decir que cambió convierte un hilo en algo que no se puede leer con confianza.
/// </summary>
public sealed class Comment : AggregateRoot, ITenantEntity
{
    public const int MaxLength = 5000;

    public Guid TenantId { get; private set; }

    /// <summary>Uno de <see cref="CommentableEntityTypes"/>.</summary>
    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public Guid AuthorId { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Cuándo se editó por última vez, o <c>null</c> si nunca se tocó.</summary>
    public DateTime? EditedAtUtc { get; private set; }

    /// <summary>
    /// Comentario al que responde, si es una respuesta.
    ///
    /// **Un solo nivel**, igual que las subtareas: hay comentarios y respuestas, y no respuestas
    /// de respuestas. Es lo que permite pintar el hilo con una cuenta y no con un recorrido de
    /// árbol, y evita de raíz los hilos que se van a la derecha hasta no caber.
    /// </summary>
    public Guid? ReplyToId { get; private set; }

    private Comment() { }

    public static Comment Create(
        Guid tenantId, string entityType, Guid entityId, Guid authorId, string text,
        Guid? replyToId = null)
    {
        if (!CommentableEntityTypes.Exists(entityType))
            throw new InvalidOperationException(Rules.UnknownEntity);

        if (entityId == Guid.Empty)
            throw new InvalidOperationException(Rules.MissingEntity);

        if (authorId == Guid.Empty)
            throw new InvalidOperationException(Rules.MissingAuthor);

        var trimmed = Validate(text);

        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = entityType,
            EntityId = entityId,
            AuthorId = authorId,
            Text = trimmed,
            CreatedAtUtc = DateTime.UtcNow,
            ReplyToId = replyToId,
        };

        comment.RaiseDomainEvent(
            new CommentAddedEvent(comment.Id, tenantId, entityType, entityId, authorId));

        return comment;
    }

    /// <summary>
    /// Cambia el texto.
    ///
    /// **Sólo lo puede hacer quien lo escribió.** Un comentario es de quien lo firma: si otro lo
    /// puede reescribir, la firma deja de significar nada. Ni siquiera un administrador; para eso
    /// está borrarlo, que sí deja constancia de que desapareció.
    /// </summary>
    public void Edit(Guid userId, string text)
    {
        if (userId != AuthorId)
            throw new InvalidOperationException(Rules.OnlyAuthorEdits);

        Text = Validate(text);
        EditedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new CommentEditedEvent(Id, TenantId, AuthorId));
    }

    /// <summary>
    /// Si alguien puede borrarlo: su autor, o quien administre.
    ///
    /// Aquí sí entra el administrador, porque moderar es parte de su trabajo y borrar no pone
    /// palabras en boca de nadie.
    /// </summary>
    public bool CanDelete(Guid userId, string role)
        => userId == AuthorId || role == "Admin";

    private static string Validate(string text)
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
        public const string TextRequired = "El comentario no puede estar vacío";
        public static readonly string TextTooLong =
            $"Un comentario no puede pasar de {MaxLength} caracteres";
        public const string UnknownEntity =
            "Sólo se puede comentar sobre una tarea, un ticket, un proyecto o una anotación";
        public const string MissingEntity = "Falta sobre qué se comenta";
        public const string MissingAuthor = "Un comentario necesita autor";
        public const string OnlyAuthorEdits = "Sólo quien escribió un comentario puede editarlo";
        public const string OnlyAuthorOrAdminDeletes =
            "Sólo quien escribió un comentario, o quien administra, puede borrarlo";
        public const string NotFound = "Comentario no encontrado";
        public const string ReplyToReply =
            "No se puede responder a una respuesta: los hilos tienen un solo nivel";
        public const string ReplyToOtherEntity =
            "Una respuesta tiene que estar en el mismo hilo que el comentario al que responde";
    }
}
