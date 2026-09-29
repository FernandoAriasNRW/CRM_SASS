using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Repositories;
using Tags.Application.Authorization;

namespace Tags.Application.Commands;

/// <summary>
/// Borrado definitivo, sin papelera: una etiqueta es configuración y hoy no la referencia nada (las
/// fichas de tarea y ticket guardan claves propias, ver docs/AUDITORIA.md §14). Cuando las
/// entidades se etiqueten con estas, borrar tendrá que soltarlas también.
///
/// Una predefinida borrada no vuelve al arrancar: el aprovisionamiento recuerda qué entregó
/// (<c>ProvisionedBuiltInTag</c>), no qué hay.
/// </summary>
public sealed class DeleteTagHandler(ITagRepository tags, IEntityPermissionService permissions)
    : ICommandHandler<DeleteTagCommand>
{
    public async Task<Result<bool>> Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await tags.GetByIdAsync(request.TenantId, request.TagId, cancellationToken)
            ?? throw new KeyNotFoundException($"No existe la etiqueta {request.TagId}");

        await TagAccess.EnsureCanManageAsync(permissions, tag, request.TenantId, request.UserId, cancellationToken);

        if (tag.IsAutomatic)
            throw new InvalidOperationException("Las etiquetas de equipos y proyectos siguen a su equipo o proyecto y no se borran a mano");

        await tags.RemoveAsync(tag, cancellationToken);
        return Result<bool>.Success(true);
    }
}
