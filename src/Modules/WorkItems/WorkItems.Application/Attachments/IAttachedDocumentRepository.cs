using WorkItems.Domain.Entities;

namespace WorkItems.Application.Attachments;

public interface IAttachedDocumentRepository
{
    Task<AttachedDocument?> GetAsync(Guid tenantId, Guid taskId, Guid documentId, CancellationToken ct = default);

    /// <summary>Los documentos adjuntos a una tarea, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<AttachedDocument>> GetForTaskAsync(Guid tenantId, Guid taskId, CancellationToken ct = default);

    /// <summary>Las tareas que tienen adjunto un documento, con lo justo para enlazarlas.</summary>
    Task<IReadOnlyList<TaskWithDocumentDto>> GetTasksForDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct = default);

    Task AddAsync(AttachedDocument attachment, CancellationToken ct = default);

    void Remove(AttachedDocument attachment);
}
