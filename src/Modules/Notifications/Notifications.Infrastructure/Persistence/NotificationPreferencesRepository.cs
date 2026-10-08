using Microsoft.EntityFrameworkCore;
using Notifications.Application.Preferences;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

public sealed class NotificationPreferencesRepository(NotificationsDbContext db) : INotificationPreferencesRepository
{
    /// <summary>
    /// Las de una persona, o <c>null</c> si nunca las ha guardado.
    ///
    /// El filtro por inquilino ya lo aplica el contexto, pero se repite aquí por escrito: es la
    /// diferencia entre depender de un filtro global que alguien puede desactivar sin querer y
    /// decir en la consulta qué se busca. Cuesta una condición.
    /// </summary>
    public Task<NotificationPreferences?> GetForUserAsync(Guid tenantId, Guid userId, CancellationToken ct) =>
        db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.UserId == userId, ct);

    public async Task<IReadOnlyDictionary<Guid, NotificationPreferences>> GetForUsersAsync(
        Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, NotificationPreferences>();

        var ids = userIds.Distinct().ToArray();
        using var _ = db.AsTenant(tenantId);

        var rows = await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.TenantId == tenantId && EF.Constant(ids).Contains(p.UserId))
            .ToListAsync(ct);

        return rows.ToDictionary(p => p.UserId);
    }

    public async Task AddAsync(NotificationPreferences preferences, CancellationToken ct) =>
        await db.NotificationPreferences.AddAsync(preferences, ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
