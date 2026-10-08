namespace WorkItems.Application.Attachments;

/// <summary>Un documento adjunto, tal como lo enseña la ficha de la tarea.</summary>
public sealed record AttachedDocumentDto(
    Guid DocumentId,
    string Title,
    DateTime DocumentUpdatedAtUtc,
    Guid AttachedById,
    DateTime AttachedAtUtc);

/// <summary>Una tarea que tiene adjunto un documento, tal como la enseña el documento.</summary>
public sealed record TaskWithDocumentDto(Guid TaskId, Guid ProjectId, string Title, string Status);
