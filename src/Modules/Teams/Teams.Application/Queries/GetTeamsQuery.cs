using BuildingBlocks.Application.Abstractions;
using Teams.Domain.Entities;

namespace Teams.Application.Queries;

public sealed record GetTeamsQuery(Guid TenantId) : IQuery<IReadOnlyList<TeamDto>>;
public sealed record GetMyTeamsQuery(Guid TenantId, Guid UserId) : IQuery<IReadOnlyList<TeamDto>>;
public sealed record GetTeamByIdQuery(Guid TenantId, Guid TeamId) : IQuery<TeamDto>;

public record TeamDto(Guid Id, string Name, string Description, int MemberCount);
