using MediatR;
using Reporting.Domain.Entities;

namespace Reporting.Application.Dashboards.Commands;

public record CreateDashboardCommand(
    Guid TenantId,
    string Title,
    bool IsDefault,
    bool IsPublic,
    Guid CreatedById,
    string WidgetsJson,
    List<Guid> TagIds) : IRequest<Guid>;

public class CreateDashboardCommandHandler : IRequestHandler<CreateDashboardCommand, Guid>
{
    private readonly ICustomDashboardRepository _repository;
    private readonly BuildingBlocks.Application.Abstractions.ITagCatalog _tagCatalog;

    public CreateDashboardCommandHandler(
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

    public async Task<Guid> Handle(CreateDashboardCommand request, CancellationToken cancellationToken)
    {
        var tagIds = await CheckedTagsAsync(_tagCatalog, request.TenantId, request.TagIds, cancellationToken);

        var dashboard = Dashboard.Create(
            request.TenantId,
            request.Title,
            request.IsDefault,
            request.IsPublic,
            request.CreatedById,
            request.WidgetsJson
        );

        dashboard.SetTags(tagIds);

        await _repository.AddAsync(dashboard, cancellationToken);

        return dashboard.Id;
    }
}
