using Teams.Domain.Entities;

namespace Teams.Application.Abstractions.Repositories;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid tenantId, Guid teamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetTeamsForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Team team, CancellationToken cancellationToken = default);
    Task UpdateAsync(Team team, CancellationToken cancellationToken = default);
}
