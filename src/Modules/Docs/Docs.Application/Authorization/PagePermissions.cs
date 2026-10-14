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
    public static Task EnsureCanWriteAsync(
        this IEntityPermissionService permissions, IUserContext user, Page page, CancellationToken ct)
        => permissions.EnsureOnDocumentAsync(user, page.DocumentId, "Write", ct);

    /// <summary>
    /// El nivel pedido sobre un documento concreto. Lo usan también las anotaciones, que llegan
    /// con su propio identificador y no con el del documento.
    /// </summary>
    public static async Task EnsureOnDocumentAsync(
        this IEntityPermissionService permissions, IUserContext user, Guid documentId, string level, CancellationToken ct)
    {
        var allowed = await permissions.HasPermissionAsync(
            user.TenantId, user.UserId, "Document", documentId, level, ct);

        // La misma excepción que el comportamiento de autorización, para que la respuesta sea la
        // misma: 403.
        if (!allowed)
            throw new UnauthorizedAccessException($"No tienes permisos de {level} para este recurso.");
    }
}
