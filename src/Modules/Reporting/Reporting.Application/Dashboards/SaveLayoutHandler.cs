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

public sealed class SaveLayoutHandler(
    ICustomDashboardRepository dashboards,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<SaveLayoutCommand, bool>
{
    public async Task<Result<bool>> Handle(SaveLayoutCommand request, CancellationToken ct)
    {
        var panel = await dashboards.GetByIdAsync(request.TenantId, request.PanelId, ct);
        if (panel is null)
            return Result<bool>.Failure("Ese panel no existe");

        // Sólo su dueño lo recoloca. Sin esto, un panel compartido lo movería cualquiera que lo
        // abriese, y quien lo montó vería su pantalla cambiada sin haber tocado nada.
        if (panel.CreatedById != request.UserId)
            return Result<bool>.Failure("Este panel es de otra persona");

        var result = panel.Place(new DashboardLayout(request.Widgets));
        if (result.IsFailure)
            return Result<bool>.Failure(result.Error!);

        await dashboards.UpdateAsync(panel, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
