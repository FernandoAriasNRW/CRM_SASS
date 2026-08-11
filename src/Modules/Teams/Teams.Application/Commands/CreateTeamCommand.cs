using BuildingBlocks.Application.Abstractions;

namespace Teams.Application.Commands;

public sealed record CreateTeamCommand(
    Guid TenantId,
    string Name,
    string Description,
    List<Guid> MemberIds) : ICommand<Guid>;
