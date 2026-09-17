using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Domain.Entities;

namespace Identity.Application.Favorites;

/// <summary>Devuelve cómo quedó: <c>true</c> si ahora está marcado.</summary>
public sealed class ToggleFavoriteHandler(IFavoriteRepository repository)
    : ICommandHandler<ToggleFavoriteCommand, bool>
{
    public async Task<Result<bool>> Handle(ToggleFavoriteCommand request, CancellationToken ct)
    {
        if (!EntityTypes.Exists(request.EntityType))
            return Result<bool>.Failure(Favorite.Rules.UnknownType);

        var existing = await repository.FindAsync(
            request.TenantId, request.UserId, request.EntityType, request.EntityId, ct);

        if (existing is not null)
        {
            repository.Remove(existing);
            await repository.SaveChangesAsync(ct);
            return Result<bool>.Success(false);
        }

        // El tope se comprueba antes de insertar. Sin él, el filtro por favoritos acabaría
        // construyendo una consulta con miles de identificadores dentro.
        var count = await repository.CountAsync(request.TenantId, request.UserId, request.EntityType, ct);

        if (count >= Favorite.MaxPerUserAndType)
            return Result<bool>.Failure(Favorite.Rules.TooMany);

        Favorite created;
        try
        {
            created = Favorite.Mark(request.TenantId, request.UserId, request.EntityType, request.EntityId);
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(ex.Message);
        }

        await repository.AddAsync(created, ct);
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
