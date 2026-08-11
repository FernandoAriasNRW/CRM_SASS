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

    public Task UpdateAsync(Team team, CancellationToken cancellationToken = default)
    {
        dbContext.Teams.Update(team);
        return Task.CompletedTask;
    }
}
