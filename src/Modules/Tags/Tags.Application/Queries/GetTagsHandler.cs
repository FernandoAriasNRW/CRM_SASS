using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Queries;
using Tags.Application.Authorization;
using Tags.Application.BuiltIn;
using Tags.Application.DTOs;
using Tags.Domain.ValueObjects;

namespace Tags.Application.Queries;

public sealed class GetTagsHandler(ITagQueries tags, IEntityPermissionService permissions)
    : IQueryHandler<GetTagsQuery, List<TagDto>>
{
    /// <summary>
    /// <c>CanManage</c> se calcula con una sola consulta de permisos para toda la lista: la de
    /// «puede gestionar todas». Un permiso concedido sobre una etiqueta suelta no se refleja aquí
    /// (la edición sí lo respeta); la pantalla de permisos sólo concede sobre todas.
    /// </summary>
    public async Task<Result<List<TagDto>>> Handle(GetTagsQuery request, CancellationToken cancellationToken)
    {
        var stored = await tags.GetByTenantAsync(request.TenantId, cancellationToken);
        var canManageAll = await TagAccess.CanManageAllAsync(permissions, request.TenantId, request.UserId, cancellationToken);

        return Result<List<TagDto>>.Success(stored
            .Select(t => t with
            {
                Name = BuiltInTags.Find(t.BuiltInKey)?.NameIn(request.Language) ?? t.Name,
                CategoryLabel = BuiltInTags.CategoryLabel(t.Category, request.Language),
                CanManage = !TagCategory.IsAutomatic(t.Category)
                    && (canManageAll || (t.CreatedBy is not null && t.CreatedBy == request.UserId)),
            })
            .ToList());
    }
}
