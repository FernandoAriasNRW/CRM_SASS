using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;

namespace Identity.Application.Sharing;

public sealed class UnshareHandler(
    ISharingRepository repository,
    IIdentityUnitOfWork unitOfWork,
    IEntityPermissionService permissions,
    IUserContext user) : ICommandHandler<UnshareCommand, bool>
{
    public async Task<Result<bool>> Handle(UnshareCommand request, CancellationToken ct)
    {
        var permissionType = PermissionTypes.FromEntityType(request.EntityType);
        await SharingRules.EnsureAsync(
            permissions, user, permissionType, request.EntityId, SharingRules.RequiredToUnshare, ct);

        var existing = await repository.FindAsync(
            request.TenantId, request.WithUserId, permissionType, request.EntityId, ct);

        // Dejar de compartir algo que ya no está compartido no es un error: es el estado que se
        // pedía. Devolver un fallo obligaría a la pantalla a distinguir dos casos idénticos.
        if (existing is null)
            return Result<bool>.Success(true);

        repository.Remove(existing);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
