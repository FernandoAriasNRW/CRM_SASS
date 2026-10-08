using Microsoft.EntityFrameworkCore;
using WorkItems.Application.Attachments;
using WorkItems.Domain.Entities;
using WorkItems.Infrastructure.Persistence;

namespace WorkItems.Infrastructure.Repositories;

public sealed class EfAttachedDocumentRepository(WorkItemsDbContext context) : IAttachedDocumentRepository
{
    public Task<AttachedDocument?> GetAsync(Guid tenantId, Guid taskId, Guid documentId, CancellationToken ct = default)
        => context.AttachedDocuments.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.TaskId == taskId && a.DocumentId == documentId, ct);

    public async Task<IReadOnlyList<AttachedDocument>> GetForTaskAsync(Guid tenantId, Guid taskId, CancellationToken ct = default)
        => await context.AttachedDocuments.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.TaskId == taskId)
            .OrderByDescending(a => a.AttachedAtUtc)
            .ToListAsync(ct);

    /// <summary>
    /// Con el filtro global de las tareas puesto: una tarea en la papelera o archivada no sale en
    /// la lista del documento, igual que no sale en el tablero.
    /// </summary>
    public async Task<IReadOnlyList<TaskWithDocumentDto>> GetTasksForDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct = default)
        => await context.AttachedDocuments.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.DocumentId == documentId)
            .Join(context.Tasks, a => a.TaskId, t => t.Id, (a, t) => new { a.AttachedAtUtc, t })
            .OrderByDescending(x => x.AttachedAtUtc)
            .Select(x => new TaskWithDocumentDto(x.t.Id, x.t.ProjectId, x.t.Title.Value, x.t.Status.Value))
            .ToListAsync(ct);

    public async Task AddAsync(AttachedDocument attachment, CancellationToken ct = default)
        => await context.AttachedDocuments.AddAsync(attachment, ct);

    public void Remove(AttachedDocument attachment) => context.AttachedDocuments.Remove(attachment);
}
