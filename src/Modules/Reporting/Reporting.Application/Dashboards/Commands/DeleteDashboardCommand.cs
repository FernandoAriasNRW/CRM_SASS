using MediatR;

namespace Reporting.Application.Dashboards.Commands;

public record DeleteDashboardCommand(Guid TenantId, Guid DashboardId, Guid UserId) : IRequest<bool>;

public class DeleteDashboardCommandHandler : IRequestHandler<DeleteDashboardCommand, bool>
{
    private readonly ICustomDashboardRepository _repository;

    public DeleteDashboardCommandHandler(ICustomDashboardRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteDashboardCommand request, CancellationToken cancellationToken)
    {
        var dashboard = await _repository.GetByIdAsync(request.TenantId, request.DashboardId, cancellationToken);
        if (dashboard == null) return false;

        if (dashboard.CreatedById != request.UserId)
            return false;

        await _repository.DeleteAsync(dashboard, cancellationToken);
        return true;
    }
}
