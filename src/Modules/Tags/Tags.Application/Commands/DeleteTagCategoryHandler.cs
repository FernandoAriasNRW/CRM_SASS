using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Authorization;
using BuildingBlocks.Domain;
using Tags.Application.Abstractions.Repositories;
using Tags.Application.Authorization;

namespace Tags.Application.Commands;

public sealed class DeleteTagCategoryHandler(ITagCategoryRepository categories, IEntityPermissionService permissions)
    : ICommandHandler<DeleteTagCategoryCommand>
{
    /// <summary>404 si no existe, 403 si no puede, 409 si todavía tiene etiquetas.</summary>
    public async Task<Result<bool>> Handle(DeleteTagCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.TenantId, request.CategoryId, cancellationToken)
            ?? throw new KeyNotFoundException($"No existe la categoría {request.CategoryId}");

        await TagAccess.EnsureCanManageAllAsync(permissions, request.TenantId, request.UserId, cancellationToken);

        var count = await categories.CountTagsAsync(request.TenantId, category.Name, cancellationToken);
        if (count > 0)
            throw new InvalidOperationException(
                $"«{category.Name}» tiene {count} etiqueta(s). Muévelas a otra categoría o bórralas antes de borrarla.");

        await categories.RemoveAsync(category, cancellationToken);
        return Result<bool>.Success(true);
    }
}
