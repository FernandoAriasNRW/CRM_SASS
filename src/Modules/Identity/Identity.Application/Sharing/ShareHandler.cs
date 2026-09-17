using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Identity.Application.Abstractions;
using Identity.Application.Abstractions.Repositories;
using Identity.Domain.Entities;
using Identity.Domain.Permissions;

namespace Identity.Application.Sharing;

public sealed class ShareHandler(
    ISharingRepository repository,
    IIdentityUnitOfWork unitOfWork) : ICommandHandler<ShareCommand, bool>
{
    /// <summary>Los niveles que la tabla de permisos entiende; cualquier otro se rechaza.</summary>
    private static readonly string[] ValidLevels = ["View", "Edit", "Full"];

    public async Task<Result<bool>> Handle(ShareCommand request, CancellationToken ct)
    {
        if (!EntityTypes.Exists(request.EntityType))
            return Result<bool>.Failure("Ese tipo de elemento no se puede compartir");

        if (!ValidLevels.Contains(request.Level))
            return Result<bool>.Failure($"El nivel debe ser uno de: {string.Join(", ", ValidLevels)}");

        if (request.EntityId == Guid.Empty)
            return Result<bool>.Failure("Falta el elemento que se quiere compartir");

        var permissionType = PermissionTypes.FromEntityType(request.EntityType);

        var existing = await repository.FindAsync(
            request.TenantId, request.WithUserId, permissionType, request.EntityId, ct);

        if (existing is not null)
        {
            // Compartir dos veces con la misma persona cambia el nivel, no crea una fila
            // paralela. Con dos filas, cuál gana dependería del orden de lectura.
            existing.UpdatePermissionLevel(request.Level);
        }
        else
        {
            await repository.AddAsync(
                EntityPermission.CreateForUser(
                    request.TenantId, request.WithUserId, permissionType, request.EntityId, request.Level),
                ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
