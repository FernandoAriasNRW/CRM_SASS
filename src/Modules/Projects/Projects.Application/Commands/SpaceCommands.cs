using BuildingBlocks.Application.Abstractions;
using Projects.Domain.Entities;

namespace Projects.Application.Commands;

public sealed record CreateSpaceCommand(Guid TenantId, string Name, string Description, string Color) : ICommand<Space>;
public sealed record UpdateSpaceCommand(Guid TenantId, Guid SpaceId, string Name, string Description, string Color) : ICommand<bool>;
public sealed record DeleteSpaceCommand(Guid TenantId, Guid SpaceId, Guid DeletedBy) : ICommand<bool>;
