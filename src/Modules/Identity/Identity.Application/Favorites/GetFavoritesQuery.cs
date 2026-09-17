using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Domain.Entities;

namespace Identity.Application.Favorites;

public sealed record GetFavoritesQuery(Guid TenantId, Guid UserId, string EntityType)
    : IQuery<IReadOnlyList<Guid>>;
