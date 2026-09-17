using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;

namespace Identity.Application.Sharing;

/// <summary>Con quién está compartido, para pintarlo.</summary>
public sealed record GetSharedWithQuery(
    Guid TenantId,
    string EntityType,
    Guid EntityId) : IQuery<IReadOnlyList<Guid>>;
