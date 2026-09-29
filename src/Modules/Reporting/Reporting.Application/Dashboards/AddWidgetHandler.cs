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

public sealed class AddWidgetHandler(
    ICustomDashboardRepository dashboards,
    IReportRepository reports,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<AddWidgetCommand, Widget>
{
    public async Task<Result<Widget>> Handle(AddWidgetCommand request, CancellationToken ct)
    {
        var panel = await dashboards.GetByIdAsync(request.TenantId, request.PanelId, ct);
        if (panel is null)
            return Result<Widget>.Failure("Ese panel no existe");

        if (panel.CreatedById != request.UserId)
            return Result<Widget>.Failure("Este panel es de otra persona");

        var report = await reports.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (report is null)
            return Result<Widget>.Failure("El informe no existe");

        var current = panel.ReadLayout().Placed;

        // Va abajo del todo y a media anchura: aparece donde se mira al terminar de añadirlo, sin
        // desplazar nada de lo que ya estaba colocado.
        var nextRow = current.Count == 0 ? 0 : current.Max(w => w.Y + w.Height);

        var widget = new Widget(
            Id: Guid.NewGuid(),
            ReportId: request.ReportId,
            X: 0, Y: nextRow,
            Width: 6, Height: 4,
            Visualization: request.Visualization ?? report.ReadDefinition()?.Visualization,
            Title: report.Name);

        var result = panel.Place(new DashboardLayout([.. current, widget]));
        if (result.IsFailure)
            return Result<Widget>.Failure(result.Error!);

        await dashboards.UpdateAsync(panel, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Widget>.Success(widget);
    }
}
