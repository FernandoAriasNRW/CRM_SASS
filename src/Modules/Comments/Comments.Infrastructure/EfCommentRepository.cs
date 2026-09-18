using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Infrastructure.Outbox;
using BuildingBlocks.Infrastructure.Persistence;
using Comments.Application;
using Comments.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Comments.Infrastructure;

public sealed class EfCommentRepository(CommentsDbContext context) : ICommentRepository
{
  public async Task<Comment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
      => await context.Comments.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

  public async Task<IReadOnlyList<Comment>> GetThreadAsync(
      Guid tenantId, string entityType, Guid entityId, CancellationToken ct = default)
      => await context.Comments.AsNoTracking()
          .Where(c => c.TenantId == tenantId && c.EntityType == entityType && c.EntityId == entityId)
          .OrderBy(c => c.CreatedAtUtc)
          .ToListAsync(ct);

  public async Task AddAsync(Comment comment, CancellationToken ct = default)
      => await context.Comments.AddAsync(comment, ct);

  public Task UpdateAsync(Comment comment, CancellationToken ct = default)
  {
    context.Comments.Update(comment);
    return Task.CompletedTask;
  }

  public Task RemoveAsync(Comment comment, CancellationToken ct = default)
  {
    context.Comments.Remove(comment);
    return Task.CompletedTask;
  }
}
