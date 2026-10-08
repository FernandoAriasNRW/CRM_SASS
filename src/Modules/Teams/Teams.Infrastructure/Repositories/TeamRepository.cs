using Microsoft.EntityFrameworkCore;
using Teams.Application.Abstractions.Repositories;
using Teams.Domain.Entities;
using Teams.Infrastructure.Persistence;

namespace Teams.Infrastructure.Repositories;

public sealed class TeamRepository(TeamsDbContext dbContext) : ITeamRepository
{
    public async Task<Team?> GetByIdAsync(Guid tenantId, Guid teamId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == teamId, cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Teams
            .Include(t => t.Members)
            .Where(t => t.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Team>> GetTeamsForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Teams
            .Include(t => t.Members)
            .Where(t => t.TenantId == tenantId && t.Members.Any(m => m.UserId == userId && !m.IsDeleted))
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Team team, CancellationToken cancellationToken = default)
    {
        dbContext.Teams.Add(team);
        return Task.CompletedTask;
    }

    /// <summary>
    /// El equipo llega ya seguido por el contexto (lo cargó <see cref="GetByIdAsync"/>), así que
    /// los cambios se detectan solos. Lo que no se puede es llamar a <c>Update</c>: recorre el grafo y
    /// marca como modificado todo lo que tiene clave, también al miembro recién añadido, cuyo
    /// <c>Guid</c> pone el dominio. EF mandaba un UPDATE de una fila que no existía, que no tocaba
    /// nada, y la edición acababa en <c>DbUpdateConcurrencyException</c>.
    /// </summary>
    public Task UpdateAsync(Team team, CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(team).State == EntityState.Detached)
            dbContext.Teams.Update(team);

        return Task.CompletedTask;
    }
}
