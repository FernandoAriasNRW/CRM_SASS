using BuildingBlocks.Domain.Primitives;

namespace WorkItems.Domain.Entities;

/// <summary>
/// Un documento adjunto a una tarea: el acta de la reunión, la especificación, la guía.
///
/// Es una relación propia y no una colección dentro de <see cref="WorkTask"/>, por lo mismo que
/// <see cref="TaskDependency"/>: se consulta desde los dos lados —qué documentos tiene la tarea, en
/// qué tareas está el documento— y colgarla de la tarea obligaría a cargarla entera para lo
/// segundo. Guarda el identificador del documento, no su título: el título es de Docs y puede
/// cambiar, y se pregunta al enseñarlo.
///
/// Adjuntar no es mencionar. Mencionar una tarea desde un documento es hablar de ella; adjuntar
/// un documento a una tarea es decir que forma parte de su trabajo.
/// </summary>
public sealed class AttachedDocument : Entity, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid AttachedById { get; private set; }
    public DateTime AttachedAtUtc { get; private set; }

    private AttachedDocument() { }

    public static AttachedDocument Create(DateTime nowUtc, Guid tenantId, Guid taskId, Guid documentId, Guid attachedById)
    {
        if (taskId == Guid.Empty || documentId == Guid.Empty)
            throw new InvalidOperationException("La tarea y el documento son obligatorios");

        return new AttachedDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskId = taskId,
            DocumentId = documentId,
            AttachedById = attachedById,
            AttachedAtUtc = nowUtc,
        };
    }

    public static class Rules
    {
        public const string TaskNotFound = "La tarea no existe";
        public const string DocumentNotFound = "El documento no existe en la organización";
    }
}
