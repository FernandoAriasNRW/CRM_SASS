using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;

namespace Identity.Application.Sharing;

/// <summary>Dejar de compartir con alguien.</summary>
public sealed record UnshareCommand(
    Guid TenantId,
    string EntityType,
    Guid EntityId,
    Guid WithUserId) : ICommand<bool>;
