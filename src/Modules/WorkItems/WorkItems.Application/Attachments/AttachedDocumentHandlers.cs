using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using WorkItems.Application.Abstractions;
using WorkItems.Application.Abstractions.Repositories;
using WorkItems.Domain.Entities;

namespace WorkItems.Application.Attachments;

public sealed class AttachDocumentHandler(
    TimeProvider timeProvider,
    ITaskRepository tasks,
    IAttachedDocumentRepository attachments,
    IDocumentCatalog documents,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<AttachDocumentCommand, AttachedDocumentDto>
{
    public async Task<Result<AttachedDocumentDto>> Handle(AttachDocumentCommand request, CancellationToken cancellationToken)
    {
        if (await tasks.GetByIdAsync(request.TenantId, request.TaskId, cancellationToken) is null)
            return Result<AttachedDocumentDto>.Failure(AttachedDocument.Rules.TaskNotFound);

        var document = (await documents.GetAsync(request.TenantId, [request.DocumentId], cancellationToken)).FirstOrDefault();
        if (document is null)
            return Result<AttachedDocumentDto>.Failure(AttachedDocument.Rules.DocumentNotFound);

        // Adjuntar dos veces el mismo documento no crea dos filas: la tarea tiene ese documento o
        // no lo tiene. La base lo garantiza además con un índice único.
        var attachment = await attachments.GetAsync(request.TenantId, request.TaskId, request.DocumentId, cancellationToken);
        if (attachment is null)
        {
            attachment = AttachedDocument.Create(
                timeProvider.GetUtcNow().UtcDateTime, request.TenantId, request.TaskId, request.DocumentId, request.AttachedById);
            await attachments.AddAsync(attachment, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<AttachedDocumentDto>.Success(new AttachedDocumentDto(
            document.Id, document.Title, document.UpdatedAtUtc, attachment.AttachedById, attachment.AttachedAtUtc));
    }
}

public sealed class DetachDocumentHandler(
    IAttachedDocumentRepository attachments,
    IWorkItemsUnitOfWork unitOfWork) : ICommandHandler<DetachDocumentCommand, bool>
{
    public async Task<Result<bool>> Handle(DetachDocumentCommand request, CancellationToken cancellationToken)
    {
        var attachment = await attachments.GetAsync(request.TenantId, request.TaskId, request.DocumentId, cancellationToken);
        if (attachment is null)
            return Result<bool>.Success(false);

        attachments.Remove(attachment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}

public sealed class GetAttachedDocumentsHandler(
    IAttachedDocumentRepository attachments,
    IDocumentCatalog documents) : IQueryHandler<GetAttachedDocumentsQuery, IReadOnlyList<AttachedDocumentDto>>
{
    public async Task<Result<IReadOnlyList<AttachedDocumentDto>>> Handle(GetAttachedDocumentsQuery request, CancellationToken cancellationToken)
    {
        var attached = await attachments.GetForTaskAsync(request.TenantId, request.TaskId, cancellationToken);
        var found = (await documents.GetAsync(request.TenantId, attached.Select(a => a.DocumentId).ToList(), cancellationToken))
            .ToDictionary(d => d.Id);

        // Un documento borrado en Docs no sale: no hay nada que abrir. La fila se queda, y si el
        // documento se restaura vuelve a aparecer en su sitio.
        IReadOnlyList<AttachedDocumentDto> result = attached
            .Where(a => found.ContainsKey(a.DocumentId))
            .Select(a =>
            {
                var document = found[a.DocumentId];
                return new AttachedDocumentDto(document.Id, document.Title, document.UpdatedAtUtc, a.AttachedById, a.AttachedAtUtc);
            })
            .ToList();

        return Result<IReadOnlyList<AttachedDocumentDto>>.Success(result);
    }
}

public sealed class GetTasksWithDocumentHandler(IAttachedDocumentRepository attachments)
    : IQueryHandler<GetTasksWithDocumentQuery, IReadOnlyList<TaskWithDocumentDto>>
{
    public async Task<Result<IReadOnlyList<TaskWithDocumentDto>>> Handle(GetTasksWithDocumentQuery request, CancellationToken cancellationToken)
        => Result<IReadOnlyList<TaskWithDocumentDto>>.Success(
            await attachments.GetTasksForDocumentAsync(request.TenantId, request.DocumentId, cancellationToken));
}
