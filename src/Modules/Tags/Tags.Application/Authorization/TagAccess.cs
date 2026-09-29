using BuildingBlocks.Application.Authorization;
using Tags.Domain.Entities;

namespace Tags.Application.Authorization;

/// <summary>
/// Quién puede editar y borrar una etiqueta. <b>Una sola regla, en un solo sitio</b>, porque la
/// usan la edición, el borrado y el listado (para decir a la pantalla qué botones enseñar):
/// <list type="bullet">
/// <item>quien la creó;</item>
/// <item>un administrador de la organización;</item>
/// <item>alguien a quien un administrador le haya dado el permiso <c>Full</c> sobre
/// <see cref="EntityType"/> desde la pantalla de permisos de la persona.</item>
/// </list>
/// Las dos últimas las resuelve <see cref="IEntityPermissionService"/>, que ya da por bueno a
/// cualquier administrador y mira los permisos concedidos a la persona. Se pide el nivel
/// <see cref="ManagePermission"/> y no <c>Write</c>: los miembros tienen <c>Write</c> por defecto,
/// que es lo que les deja crear etiquetas, y no deben poder tocar las de los demás.
/// </summary>
public static class TagAccess
{
    /// <summary>El tipo en la tabla de permisos. Es el mismo que piden los comandos de alta.</summary>
    public const string EntityType = "Tag";

    public const string ManagePermission = "Admin";

    /// <summary>
    /// Si puede gestionar todas las etiquetas, no sólo las suyas: administrador o permiso concedido
    /// sobre el tipo entero (<c>Guid.Empty</c>), que es lo que guarda la pantalla de permisos.
    /// </summary>
    public static Task<bool> CanManageAllAsync(
        IEntityPermissionService permissions, Guid tenantId, Guid userId, CancellationToken cancellationToken)
        => permissions.HasPermissionAsync(tenantId, userId, EntityType, Guid.Empty, ManagePermission, cancellationToken);

    /// <summary>Lanza <see cref="UnauthorizedAccessException"/> (un 403) si no puede.</summary>
    public static async Task EnsureCanManageAsync(
        IEntityPermissionService permissions, Tag tag, Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        if (userId != Guid.Empty && tag.CreatedBy == userId)
            return;

        // Con el id de la etiqueta y no Guid.Empty: el servicio acepta tanto un permiso sobre esta
        // etiqueta como uno sobre todas.
        if (await permissions.HasPermissionAsync(tenantId, userId, EntityType, tag.Id, ManagePermission, cancellationToken))
            return;

        throw new UnauthorizedAccessException(
            "Sólo quien creó la etiqueta, un administrador o alguien con permiso para gestionar etiquetas puede cambiarla o borrarla.");
    }
}
