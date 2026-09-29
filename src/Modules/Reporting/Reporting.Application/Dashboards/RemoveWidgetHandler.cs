using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Application.Dashboards;
using Reporting.Application.Definitions;
using Reporting.Domain.Entities;
using Reporting.Domain.Dashboards;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Dashboards;

public sealed class RemoveWidgetHandler(
    ICustomDashboardRepository dashboards,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<RemoveWidgetCommand, bool>
{
    public async Task<Result<bool>> Handle(RemoveWidgetCommand request, CancellationToken ct)
    {
        var panel = await dashboards.GetByIdAsync(request.TenantId, request.PanelId, ct);
        if (panel is null)
            return Result<bool>.Failure("Ese panel no existe");

        if (panel.CreatedById != request.UserId)
            return Result<bool>.Failure("Este panel es de otra persona");

        var remaining = panel.ReadLayout().Placed.Where(w => w.Id != request.WidgetId).ToList();

        // Quitar el informe al que apunta sería destruir trabajo por un gesto de colocación:
        // sacar algo del panel no es borrarlo, y el informe sigue en su lista.
        var result = panel.Place(new DashboardLayout(remaining));
        if (result.IsFailure)
            return Result<bool>.Failure(result.Error!);

        await dashboards.UpdateAsync(panel, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
