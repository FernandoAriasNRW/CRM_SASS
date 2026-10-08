using BuildingBlocks.Application.Abstractions;
using Teams.Domain.Entities;

namespace Teams.Application.Queries;

public sealed record GetTeamsQuery(Guid TenantId) : IQuery<IReadOnlyList<TeamDto>>;
public sealed record GetMyTeamsQuery(Guid TenantId, Guid UserId) : IQuery<IReadOnlyList<TeamDto>>;
public sealed record GetTeamByIdQuery(Guid TenantId, Guid TeamId) : IQuery<TeamDto>;

/// <summary>
/// Un equipo. <c>MemberIds</c> son los miembros activos: sin ellos la pantalla de edición no podía
/// marcar a nadie, y guardar habría dejado el equipo vacío en cuanto la API aplicara la lista.
/// </summary>
public record TeamDto(Guid Id, string Name, string Description, int MemberCount, IReadOnlyList<Guid> MemberIds)
{
    public static TeamDto FromDomain(Team team)
    {
        var memberIds = team.ActiveMemberIds;
        return new TeamDto(team.Id, team.Name, team.Description, memberIds.Count, memberIds);
    }
}
