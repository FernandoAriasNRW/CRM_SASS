using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Comments.Domain.Entities;

namespace Comments.Application;

public interface ICommentsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
