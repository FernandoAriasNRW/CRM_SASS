using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using Docs.Domain.Entities;

namespace Docs.Application.Authorization;

/// <summary>
/// El permiso sobre el documento de una página.
///
/// Los comandos de página llegan solo con el identificador de la página, así que el
/// <c>AuthorizationBehavior</c> comprueba el nivel sobre los documentos en general: quien tenía
/// restringido un documento concreto podía editar, mover o borrar sus páginas igual. Aquí se
/// comprueba, ya con la página cargada, sobre su documento.
/// </summary>
internal static class PagePermissions
{
    public static async Task EnsureCanWriteAsync(
        this IEntityPermissionService permissions, IUserContext user, Page page, CancellationToken ct)
    {
        var allowed = await permissions.HasPermissionAsync(
            user.TenantId, user.UserId, "Document", page.DocumentId, "Write", ct);

        // La misma excepción que el comportamiento de autorización, para que la respuesta sea la
        // misma: 403.
        if (!allowed)
            throw new UnauthorizedAccessException("No tienes permisos de Write para este recurso.");
    }
}
