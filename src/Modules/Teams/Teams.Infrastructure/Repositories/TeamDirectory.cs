using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Teams.Infrastructure.Persistence;

namespace Teams.Infrastructure.Repositories;

/// <summary>
/// Responde el puerto <see cref="ITeamDirectory"/>. Los miembros dados de baja no cuentan: los
/// esconde el filtro global de borrado lógico, igual que en la ficha del equipo.
/// </summary>
internal sealed class TeamDirectory(TeamsDbContext context) : ITeamDirectory
{
    public async Task<IReadOnlyList<Guid>?> GetMemberIdsAsync(Guid tenantId, Guid teamId, CancellationToken ct = default)
    {
        var team = await context.Teams.AsNoTracking()
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == teamId, ct);

        return team?.ActiveMemberIds;
    }

    public async Task<IReadOnlyList<Guid>> GetTeamMateIdsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var teams = await context.Teams.AsNoTracking()
            .Include(t => t.Members)
            .Where(t => t.TenantId == tenantId && t.Members.Any(m => m.UserId == userId && !m.IsDeleted))
            .ToListAsync(ct);

        return teams.SelectMany(t => t.ActiveMemberIds).Distinct().ToList();
    }
}
