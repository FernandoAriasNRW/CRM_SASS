using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;

namespace Identity.Application.Sharing;

public sealed class UnshareHandler(
    ISharingRepository repository,
    IIdentityUnitOfWork unitOfWork) : ICommandHandler<UnshareCommand, bool>
{
    public async Task<Result<bool>> Handle(UnshareCommand request, CancellationToken ct)
    {
        var existing = await repository.FindAsync(
            request.TenantId, request.WithUserId,
            PermissionTypes.FromEntityType(request.EntityType), request.EntityId, ct);

        // Dejar de compartir algo que ya no está compartido no es un error: es el estado que se
        // pedía. Devolver un fallo obligaría a la pantalla a distinguir dos casos idénticos.
        if (existing is null)
            return Result<bool>.Success(true);

        repository.Remove(existing);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
