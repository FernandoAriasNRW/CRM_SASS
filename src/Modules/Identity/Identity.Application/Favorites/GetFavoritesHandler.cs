using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Domain.Entities;

namespace Identity.Application.Favorites;

public sealed class GetFavoritesHandler(IFavoriteRepository repository)
    : IQueryHandler<GetFavoritesQuery, IReadOnlyList<Guid>>
{
    public async Task<Result<IReadOnlyList<Guid>>> Handle(GetFavoritesQuery request, CancellationToken ct)
    {
        if (!EntityTypes.Exists(request.EntityType))
            return Result<IReadOnlyList<Guid>>.Failure(Favorite.Rules.UnknownType);

        return Result<IReadOnlyList<Guid>>.Success(
            await repository.GetIdsAsync(request.TenantId, request.UserId, request.EntityType, ct));
    }
}
