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

    public UpdateDashboardCommandHandler(ICustomDashboardRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(UpdateDashboardCommand request, CancellationToken cancellationToken)
    {
        var dashboard = await _repository.GetByIdAsync(request.TenantId, request.DashboardId, cancellationToken);
        if (dashboard == null) return false;

        // Permissions logic: Only the creator (or an admin, handled at endpoint) can edit
        if (dashboard.CreatedById != request.UserId)
            return false;

        dashboard.Update(request.Title, request.IsDefault, request.IsPublic, request.WidgetsJson);

        // Update tags
        foreach (var tagId in dashboard.TagIds.ToList())
        {
            if (request.TagIds == null || !request.TagIds.Contains(tagId))
                dashboard.RemoveTag(tagId);
        }

        foreach (var tagId in request.TagIds ?? new List<Guid>())
        {
            dashboard.AddTag(tagId);
        }

        await _repository.UpdateAsync(dashboard, cancellationToken);
        return true;
    }
}
