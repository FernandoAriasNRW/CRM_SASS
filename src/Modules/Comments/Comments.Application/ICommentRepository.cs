using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public interface ICommentRepository
{
    Task<Comment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>El hilo de una entidad, del más antiguo al más nuevo: se lee en orden.</summary>
    Task<IReadOnlyList<Comment>> GetThreadAsync(
        Guid tenantId, string entityType, Guid entityId, CancellationToken ct = default);

    Task AddAsync(Comment comment, CancellationToken ct = default);
    Task UpdateAsync(Comment comment, CancellationToken ct = default);
    Task RemoveAsync(Comment comment, CancellationToken ct = default);
}
