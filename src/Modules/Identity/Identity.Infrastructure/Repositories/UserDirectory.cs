using BuildingBlocks.Application.Abstractions;
using Identity.Domain.ValueObjects;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

/// <summary>
/// Responde <see cref="IUserDirectory"/>. Con el inquilino en el <c>Where</c> además del filtro
/// global, y con él forzado para que funcione también sin petición (un trabajo en segundo plano que
/// avisa de algo).
/// </summary>
internal sealed class UserDirectory(IdentityDbContext context) : IUserDirectory
{
    public async Task<IReadOnlyDictionary<Guid, DirectoryUser>> GetAsync(
        Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, DirectoryUser>();

        var ids = userIds.Distinct().ToArray();
        using var _ = context.AsTenant(tenantId);

        // Se compara el rol entero: se guarda con una conversión y EF no traduce `Role.Value`.
        var users = await context.User.AsNoTracking()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted && EF.Constant(ids).Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Role })
            .ToListAsync(ct);

        return users.ToDictionary(u => u.Id, u => new DirectoryUser(u.Id, u.Name, u.Role == UserRole.Admin));
    }

    public async Task<IReadOnlyList<Guid>> GetAdminIdsAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var _ = context.AsTenant(tenantId);

        return await context.User.AsNoTracking()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted && u.Role == UserRole.Admin)
            .Select(u => u.Id)
            .ToListAsync(ct);
    }
}
