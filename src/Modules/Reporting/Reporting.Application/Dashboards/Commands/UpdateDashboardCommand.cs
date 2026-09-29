using MediatR;

namespace Reporting.Application.Dashboards.Commands;

public record UpdateDashboardCommand(
    Guid TenantId,
    Guid DashboardId,
    Guid UserId,
    string Title,
    bool IsDefault,
    bool IsPublic,
    string WidgetsJson,
    List<Guid> TagIds) : IRequest<bool>;

public class UpdateDashboardCommandHandler : IRequestHandler<UpdateDashboardCommand, bool>
{
    private readonly ICustomDashboardRepository _repository;
    private readonly BuildingBlocks.Application.Abstractions.ITagCatalog _tagCatalog;

    public UpdateDashboardCommandHandler(
        ICustomDashboardRepository repository, BuildingBlocks.Application.Abstractions.ITagCatalog tagCatalog)
    {
        _repository = repository;
        _tagCatalog = tagCatalog;
    }

    /// <summary>
    /// Las etiquetas que llegan, comprobadas. Estos comandos no devuelven <c>Result</c>: un error se
    /// lanza como <see cref="ArgumentException"/>, que el manejador global convierte en un 400.
    /// </summary>
    private static async Task<IReadOnlyList<Guid>> CheckedTagsAsync(
        BuildingBlocks.Application.Abstractions.ITagCatalog catalog, Guid tenantId, IEnumerable<Guid>? requested, CancellationToken ct)
    {
        var tagIds = BuildingBlocks.Application.Abstractions.TagIdList.Normalize(requested);
        var error = await BuildingBlocks.Application.Abstractions.TagIdList.ValidateAsync(catalog, tenantId, tagIds, ct);
        return error is null ? tagIds : throw new ArgumentException(error);
    }

    public async Task<bool> Handle(UpdateDashboardCommand request, CancellationToken cancellationToken)
    {
        var dashboard = await _repository.GetByIdAsync(request.TenantId, request.DashboardId, cancellationToken);
        if (dashboard == null) return false;

        // Permissions logic: Only the creator (or an admin, handled at endpoint) can edit
        if (dashboard.CreatedById != request.UserId)
            return false;

        var tagIds = await CheckedTagsAsync(_tagCatalog, request.TenantId, request.TagIds, cancellationToken);

        dashboard.Update(request.Title, request.IsDefault, request.IsPublic, request.WidgetsJson);
        dashboard.SetTags(tagIds);

        await _repository.UpdateAsync(dashboard, cancellationToken);
        return true;
    }
}
