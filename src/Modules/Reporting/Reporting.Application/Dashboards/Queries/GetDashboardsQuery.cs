using MediatR;
using Reporting.Domain.Entities;

namespace Reporting.Application.Dashboards.Queries;

public record GetDashboardsQuery(Guid TenantId, Guid UserId) : IRequest<List<DashboardDto>>;

public class DashboardDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsPublic { get; set; }
    public Guid CreatedById { get; set; }
    public string WidgetsJson { get; set; } = string.Empty;
    public List<Guid> TagIds { get; set; } = new();
}

public class GetDashboardsQueryHandler : IRequestHandler<GetDashboardsQuery, List<DashboardDto>>
{
    private readonly ICustomDashboardRepository _repository;

    public GetDashboardsQueryHandler(ICustomDashboardRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<DashboardDto>> Handle(GetDashboardsQuery request, CancellationToken cancellationToken)
    {
        // Lists public dashboards OR private dashboards of the user
        var dashboards = await _repository.GetDashboardsAsync(request.TenantId, request.UserId, cancellationToken);

        return dashboards.Select(d => new DashboardDto
        {
            Id = d.Id,
            Title = d.Title,
            IsDefault = d.IsDefault,
            IsPublic = d.IsPublic,
            CreatedById = d.CreatedById,
            WidgetsJson = d.WidgetsJson,
            TagIds = d.TagIds
        }).ToList();
    }
}
