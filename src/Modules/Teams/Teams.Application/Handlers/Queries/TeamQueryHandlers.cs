using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Teams.Application.Abstractions.Repositories;
using Teams.Application.Queries;

namespace Teams.Application.Handlers.Queries;

public sealed class GetTeamsQueryHandler(ITeamRepository repository) : IQueryHandler<GetTeamsQuery, IReadOnlyList<TeamDto>>
{
    public async Task<Result<IReadOnlyList<TeamDto>>> Handle(GetTeamsQuery request, CancellationToken cancellationToken)
    {
        var teams = await repository.GetAllAsync(request.TenantId, cancellationToken);
        var dtos = teams.Select(t => new TeamDto(t.Id, t.Name, t.Description, t.Members.Count)).ToList();
        return Result<IReadOnlyList<TeamDto>>.Success(dtos);
    }
}

public sealed class GetMyTeamsQueryHandler(ITeamRepository repository) : IQueryHandler<GetMyTeamsQuery, IReadOnlyList<TeamDto>>
{
    public async Task<Result<IReadOnlyList<TeamDto>>> Handle(GetMyTeamsQuery request, CancellationToken cancellationToken)
    {
        var teams = await repository.GetTeamsForUserAsync(request.TenantId, request.UserId, cancellationToken);
        var dtos = teams.Select(t => new TeamDto(t.Id, t.Name, t.Description, t.Members.Count)).ToList();
        return Result<IReadOnlyList<TeamDto>>.Success(dtos);
    }
}

public sealed class GetTeamByIdQueryHandler(ITeamRepository repository) : IQueryHandler<GetTeamByIdQuery, TeamDto>
{
    public async Task<Result<TeamDto>> Handle(GetTeamByIdQuery request, CancellationToken cancellationToken)
    {
        var team = await repository.GetByIdAsync(request.TenantId, request.TeamId, cancellationToken);
        if (team is null) return Result<TeamDto>.Failure("Team not found");
        
        var dto = new TeamDto(team.Id, team.Name, team.Description, team.Members.Count);
        return Result<TeamDto>.Success(dto);
    }
}
