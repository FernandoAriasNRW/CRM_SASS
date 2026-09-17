using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Sharing;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Responde el puerto <see cref="IEntityVisibility"/> para el usuario de la petición.
///
/// Lo implementa Identity porque es quien guarda los permisos, y lo consumen los demás módulos
/// sin conocerlo, igual que con los favoritos: la dependencia va de todos a BuildingBlocks,
/// nunca entre módulos.
/// </summary>
public sealed class EntityVisibility(
    ISharingRepository repository,
    IUserContext currentUser) : IEntityVisibility
{
    public Task<IReadOnlyList<Guid>> GetSharedWithMeAsync(string entityType, CancellationToken ct = default)
        => repository.GetSharedWithUserAsync(
            currentUser.TenantId, currentUser.UserId,
            PermissionTypes.FromEntityType(entityType), ct);

    public Task<IReadOnlyList<Guid>> GetSharedWithOthersAsync(string entityType, CancellationToken ct = default)
        => repository.GetSharedWithOthersAsync(
            currentUser.TenantId,
            PermissionTypes.FromEntityType(entityType), ct);
}
