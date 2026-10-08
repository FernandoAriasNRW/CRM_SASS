using BuildingBlocks.Application.Abstractions;

namespace WorkItems.Application.Attachments;

/// <summary>Adjunta un documento a una tarea. Si ya lo estaba, no hace nada y responde igual.</summary>
public sealed record AttachDocumentCommand(Guid TenantId, Guid TaskId, Guid DocumentId, Guid AttachedById)
    : ICommand<AttachedDocumentDto>;

/// <summary>Quita un documento de una tarea. El documento sigue existiendo en Docs.</summary>
public sealed record DetachDocumentCommand(Guid TenantId, Guid TaskId, Guid DocumentId) : ICommand<bool>;

public sealed record GetAttachedDocumentsQuery(Guid TenantId, Guid TaskId) : IQuery<IReadOnlyList<AttachedDocumentDto>>;

public sealed record GetTasksWithDocumentQuery(Guid TenantId, Guid DocumentId) : IQuery<IReadOnlyList<TaskWithDocumentDto>>;
