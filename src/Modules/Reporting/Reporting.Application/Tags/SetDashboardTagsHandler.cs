using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Dashboards;

namespace Reporting.Application.Tags;

public sealed class SetDashboardTagsHandler(ICustomDashboardRepository dashboards, ITagCatalog tagCatalog)
    : ICommandHandler<SetDashboardTagsCommand, bool>
{
    public const string DashboardNotFound = "El panel no existe";

    /// <summary>404 si no existe, 403 si no es quien lo creó ni administrador, 400 si alguna etiqueta no vale.</summary>
    public async Task<Result<bool>> Handle(SetDashboardTagsCommand request, CancellationToken ct)
    {
        var dashboard = await dashboards.GetByIdAsync(request.TenantId, request.DashboardId, ct);
        if (dashboard is null)
            return Result<bool>.Failure(DashboardNotFound);

        if (dashboard.CreatedById != request.UserId && !request.IsAdmin)
            throw new UnauthorizedAccessException("Sólo quien creó el panel o un administrador puede cambiar sus etiquetas.");

        var tagIds = TagIdList.Normalize(request.TagIds);
        var error = await TagIdList.ValidateAsync(tagCatalog, request.TenantId, tagIds, ct);
        if (error is not null)
            return Result<bool>.Failure(error);

        dashboard.SetTags(tagIds);
        await dashboards.UpdateAsync(dashboard, ct);
        return Result<bool>.Success(true);
    }
}
