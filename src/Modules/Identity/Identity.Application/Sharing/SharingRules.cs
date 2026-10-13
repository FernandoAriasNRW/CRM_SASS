using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;

namespace Identity.Application.Sharing;

/// <summary>
/// Quién puede compartir qué.
///
/// <b>Compartir no comprobaba nada.</b> Cualquiera con sesión podía compartir cualquier tarea,
/// ticket o proyecto con quien quisiera, también consigo mismo y con nivel <c>Full</c>. Y
/// dejar de compartir tampoco: quien tenía un elemento restringido (nivel <c>None</c>) podía
/// borrar su propia restricción y volver al acceso por defecto.
///
/// Ahora:
/// <list type="bullet">
/// <item>Compartir con <c>View</c> o <c>Edit</c> exige poder editar el elemento.</item>
/// <item>Compartir con <c>Full</c> exige tener <c>Full</c>: nadie da más de lo que tiene, y por
/// tanto nadie se lo sube a sí mismo.</item>
/// <item>Dejar de compartir exige <c>Full</c>, porque quitar una fila puede ser quitar una
/// restricción.</item>
/// </list>
/// Quien administra puede siempre, como en el resto de permisos (<see cref="IEntityPermissionService"/>).
/// </summary>
internal static class SharingRules
{
    public static string RequiredToShare(string level) => level == "Full" ? "Full" : "Write";

    public const string RequiredToUnshare = "Full";

    public static async Task EnsureAsync(
        IEntityPermissionService permissions, IUserContext user,
        string permissionType, Guid entityId, string required, CancellationToken ct)
    {
        var allowed = await permissions.HasPermissionAsync(
            user.TenantId, user.UserId, permissionType, entityId, required, ct);

        // La misma excepción que el comportamiento de autorización: la respuesta es un 403.
        if (!allowed)
            throw new UnauthorizedAccessException($"No tienes permisos de {required} para compartir este elemento.");
    }
}
