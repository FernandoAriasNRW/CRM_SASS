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

  /// <summary>
  /// El comentario llega ya seguido por el contexto (lo cargó <see cref="GetByIdAsync"/>), y sus
  /// cambios se detectan solos. <c>Update</c> recorre el grafo y marca como modificado todo lo que
  /// tiene clave, también las menciones recién leídas del texto, que no existen todavía: EF
  /// mandaría un UPDATE que no toca ninguna fila y la edición acabaría en
  /// <c>DbUpdateConcurrencyException</c>. Es lo mismo que pasó con los miembros de un equipo.
  /// </summary>
  public Task UpdateAsync(Comment comment, CancellationToken ct = default)
  {
    if (context.Entry(comment).State == EntityState.Detached)
      context.Comments.Update(comment);
    return Task.CompletedTask;
  }

  /// <summary>
  /// Los comentarios que mencionan algo, del más reciente al más antiguo. Con el inquilino en el
  /// <c>Where</c> además del filtro global, como en el resto de consultas.
  /// </summary>
  public async Task<IReadOnlyList<Comment>> GetMentioningAsync(
      Guid tenantId, string type, Guid entityId, int max, CancellationToken ct = default)
      => await context.Comments.AsNoTracking()
          .Where(c => c.TenantId == tenantId && c.Mentions.Any(m => m.Type == type && m.EntityId == entityId))
          .OrderByDescending(c => c.CreatedAtUtc)
          .Take(max)
          .ToListAsync(ct);

  public Task RemoveAsync(Comment comment, CancellationToken ct = default)
  {
    context.Comments.Remove(comment);
    return Task.CompletedTask;
  }
}
