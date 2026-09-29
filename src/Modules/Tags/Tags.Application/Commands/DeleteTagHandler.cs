using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Repositories;
using Tags.Application.Authorization;

namespace Tags.Application.Commands;

/// <summary>
/// Borrado definitivo, sin papelera: una etiqueta es configuración. Antes de borrarla se suelta de
/// todo lo que la lleva (tareas, tickets, proyectos, informes, dashboards), cada módulo con su
/// <see cref="ITagReferences"/>. Primero se sueltan y después se borra: si algo falla a medias, lo
/// peor que queda es una etiqueta viva sin usos, no ids colgando de una etiqueta que ya no existe.
///
/// Una predefinida borrada no vuelve al arrancar: el aprovisionamiento recuerda qué entregó
/// (<c>ProvisionedBuiltInTag</c>), no qué hay.
/// </summary>
public sealed class DeleteTagHandler(
    ITagRepository tags, IEntityPermissionService permissions, IEnumerable<ITagReferences> references)
    : ICommandHandler<DeleteTagCommand>
{
    public async Task<Result<bool>> Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await tags.GetByIdAsync(request.TenantId, request.TagId, cancellationToken)
            ?? throw new KeyNotFoundException($"No existe la etiqueta {request.TagId}");

        await TagAccess.EnsureCanManageAsync(permissions, tag, request.TenantId, request.UserId, cancellationToken);

        if (tag.IsAutomatic)
            throw new InvalidOperationException("Las etiquetas de equipos y proyectos siguen a su equipo o proyecto y no se borran a mano");

        foreach (var module in references)
            await module.RemoveTagAsync(request.TenantId, tag.Id, cancellationToken);

        await tags.RemoveAsync(tag, cancellationToken);
        return Result<bool>.Success(true);
    }
}
