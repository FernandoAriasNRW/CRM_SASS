using Identity.Application.Favorites;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Responde el puerto <see cref="BuildingBlocks.Application.Abstractions.IUserFavorites"/>
/// para el usuario de la petición en curso.
///
/// Lo implementa Identity porque es quien guarda los favoritos, y lo consumen los demás módulos
/// sin conocerlo: la dependencia va de todos a BuildingBlocks, nunca entre módulos.
/// </summary>
public sealed class UserFavorites(
    IFavoriteRepository repository,
    BuildingBlocks.Application.Abstractions.IUserContext currentUser)
    : BuildingBlocks.Application.Abstractions.IUserFavorites
{
    public Task<IReadOnlyList<Guid>> GetIdsAsync(string entityType, CancellationToken ct = default)
        => repository.GetIdsAsync(currentUser.TenantId, currentUser.UserId, entityType, ct);
}
