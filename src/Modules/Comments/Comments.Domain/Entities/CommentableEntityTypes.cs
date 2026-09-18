using BuildingBlocks.Domain;

namespace Comments.Domain.Entities;

/// <summary>
/// Sobre qué se puede comentar.
///
/// La lista es cerrada: un comentario colgado de un tipo que nadie pinta es un dato que no
/// vuelve a ver nadie. Se amplía cuando haya una pantalla que lo muestre.
///
/// Las tres entidades salen de <see cref="EntityTypes"/> y no se escriben aquí: antes estaban
/// repetidas a mano, y el día que una cambiara de valor los comentarios de esa entidad dejarían
/// de encontrarse sin ningún error.
/// </summary>
public static class CommentableEntityTypes
{
    public const string Task = EntityTypes.Task;
    public const string Ticket = EntityTypes.Ticket;
    public const string Project = EntityTypes.Project;

    /// <summary>
    /// Un comentario en línea dentro de un documento.
    ///
    /// La entidad comentada es la <b>anotación</b> —el trozo de texto señalado—, no el documento:
    /// un documento tiene muchas conversaciones a la vez, cada una pegada a un sitio distinto.
    /// Dónde está pegada lo guarda Docs; el hilo, este módulo.
    /// </summary>
    public const string Annotation = "Anotacion";

    public static IReadOnlyList<string> All() => [Task, Ticket, Project, Annotation];

    public static bool Exists(string type) => All().Contains(type);
}
