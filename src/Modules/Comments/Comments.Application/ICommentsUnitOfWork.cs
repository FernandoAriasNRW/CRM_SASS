using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public interface ICommentsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Guarda y reparte los eventos en proceso: de aquí salen los avisos de «han comentado en lo
    /// tuyo» y «te han mencionado». Ver <c>IWorkItemsUnitOfWork.SaveChangesAndDispatchAsync</c>.
    /// </summary>
    Task<int> SaveChangesAndDispatchAsync(CancellationToken ct = default);
}
