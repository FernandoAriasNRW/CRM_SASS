using Identity.Application.Favorites;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class FavoriteRepository(IdentityDbContext context) : IFavoriteRepository
{
    /// <summary>
    /// Sólo los identificadores, no las filas enteras.
    ///
    /// Es lo único que necesita el filtro —«dame las tareas cuyo id esté en esta lista»— y traer
    /// el resto de la fila sería mover datos que nadie va a mirar. Van ordenados por cuándo se
    /// marcaron, de lo más reciente a lo más antiguo, que es el orden en que la gente espera ver
    /// sus favoritos.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetIdsAsync(Guid tenantId, Guid userId, string entityType, CancellationToken ct)
        => await context.Favorites
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.UserId == userId && f.EntityType == entityType)
            .OrderByDescending(f => f.MarkedAtUtc)
            .Select(f => f.EntityId)
            .ToListAsync(ct);

    public Task<Favorite?> FindAsync(Guid tenantId, Guid userId, string entityType, Guid entityId, CancellationToken ct)
        => context.Favorites.FirstOrDefaultAsync(
            f => f.TenantId == tenantId && f.UserId == userId && f.EntityType == entityType && f.EntityId == entityId, ct);

    public Task<int> CountAsync(Guid tenantId, Guid userId, string entityType, CancellationToken ct)
        => context.Favorites.CountAsync(
            f => f.TenantId == tenantId && f.UserId == userId && f.EntityType == entityType, ct);

    public async Task AddAsync(Favorite favorite, CancellationToken ct)
        => await context.Favorites.AddAsync(favorite, ct);

    public void Remove(Favorite favorite) => context.Favorites.Remove(favorite);

    public Task SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
