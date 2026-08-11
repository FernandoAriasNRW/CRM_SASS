using BuildingBlocks.Application.Abstractions;

namespace Teams.Application.Commands;

public sealed record DeleteTeamCommand(
    Guid TenantId,
    Guid TeamId) : ICommand<bool>;
