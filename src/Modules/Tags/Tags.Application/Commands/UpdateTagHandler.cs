using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Repositories;
using Tags.Application.Authorization;
using Tags.Application.DTOs;

namespace Tags.Application.Commands;

public sealed class UpdateTagHandler(ITagRepository tags, IEntityPermissionService permissions)
    : ICommandHandler<UpdateTagCommand, TagDto>
{
    /// <summary>
    /// 404 si no existe en la organización, 403 si no puede tocarla, 409 si es de un equipo o un
    /// proyecto (lo lanza el dominio) o si el nombre ya está cogido en la categoría (el fallo que
    /// devuelve).
    /// </summary>
    public async Task<Result<TagDto>> Handle(UpdateTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await tags.GetByIdAsync(request.TenantId, request.TagId, cancellationToken)
            ?? throw new KeyNotFoundException($"No existe la etiqueta {request.TagId}");

        await TagAccess.EnsureCanManageAsync(permissions, tag, request.TenantId, request.UserId, cancellationToken);

        var name = request.Name.Trim();
        var category = request.Category.Trim();

        if (await tags.NameIsTakenAsync(request.TenantId, category, name, exceptTag: tag, cancellationToken))
            return Result<TagDto>.Failure($"Ya existe una etiqueta llamada «{name}» en esa categoría");

        tag.Edit(name, string.IsNullOrEmpty(request.ColorHex) ? tag.ColorHex : request.ColorHex.ToUpperInvariant(), category);
        await tags.SaveChangesAsync(cancellationToken);

        return Result<TagDto>.Success(TagDtoMapper.From(tag, canManage: true));
    }
}
