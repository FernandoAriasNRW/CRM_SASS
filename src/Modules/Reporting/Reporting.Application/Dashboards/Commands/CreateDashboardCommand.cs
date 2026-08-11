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

    public CreateDashboardCommandHandler(ICustomDashboardRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateDashboardCommand request, CancellationToken cancellationToken)
    {
        var dashboard = Dashboard.Create(
            request.TenantId,
            request.Title,
            request.IsDefault,
            request.IsPublic,
            request.CreatedById,
            request.WidgetsJson
        );

        foreach (var tagId in request.TagIds ?? new List<Guid>())
        {
            dashboard.AddTag(tagId);
        }

        await _repository.AddAsync(dashboard, cancellationToken);

        return dashboard.Id;
    }
}
