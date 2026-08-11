using BuildingBlocks.Application.Abstractions;

namespace Teams.Application.Commands;

public sealed record UpdateTeamCommand(
    Guid TenantId,
    Guid TeamId,
    string Name,
    string Description,
    List<Guid> MemberIds) : ICommand<bool>;
