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

public sealed class GetDashboardDataHandler(
    ICustomDashboardRepository dashboards,
    IReportRepository reports,
    IReportResolver resolutor) : IQueryHandler<GetDashboardDataQuery, IReadOnlyList<WidgetDataDto>>
{
    /// <summary>
    /// Cuántas filas se mandan por recuadro.
    ///
    /// Una gráfica de más de treinta categorías no se lee, y el motor ya junta la cola en «Otros».
    /// El tope existe para que un widget mal configurado —agrupado por un campo con miles de
    /// valores— no mande un megabyte a la pantalla.
    /// </summary>
    private const int RowsPerWidget = 30;

    public async Task<Result<IReadOnlyList<WidgetDataDto>>> Handle(
        GetDashboardDataQuery request, CancellationToken ct)
    {
        var panel = await dashboards.GetByIdAsync(request.TenantId, request.PanelId, ct);
        if (panel is null)
            return Result<IReadOnlyList<WidgetDataDto>>.Failure("Ese panel no existe");

        var data = new List<WidgetDataDto>();

        foreach (var widget in panel.ReadLayout().Placed)
        {
            data.Add(await SingleWidgetAsync(request.TenantId, widget, ct));
        }

        return Result<IReadOnlyList<WidgetDataDto>>.Success(data);
    }

    private async Task<WidgetDataDto> SingleWidgetAsync(Guid tenantId, Widget widget, CancellationToken ct)
    {
        var title = widget.Titulo ?? "Informe";
        var visualization = widget.Forma ?? "tabla";

        var report = await reports.GetByIdAsync(tenantId, widget.ReportId, ct);

        if (report is null)
        {
            // El informe se borró y el recuadro se quedó apuntando a nada. Se dice en el propio
            // recuadro para que quien lo vea sepa qué quitar, en vez de dejar un hueco mudo.
            return Empty(widget, title, visualization, "El informe de este recuadro ya no existe");
        }

        var definition = report.ReadDefinition();

        if (definition is null)
        {
            return Empty(widget, report.Name, visualization,
                "Este informe no está configurado todavía. Ábrelo en el constructor.");
        }

        try
        {
            var table = await resolutor.ResolveAsync(report.Name, tenantId, definition, ct);

            return new WidgetDataDto(
                widget.Id, widget.ReportId,
                widget.Titulo ?? report.Name,
                widget.Forma ?? definition.Forma,
                table.Subtitle, table.Columns,
                table.Rows.Take(RowsPerWidget).ToList(),
                Error: null);
        }
        catch (InvalidOperationException ex)
        {
            return Empty(widget, report.Name, visualization, ex.Message);
        }
    }

    private static WidgetDataDto Empty(Widget widget, string title, string visualization, string error)
        => new(widget.Id, widget.ReportId, title, visualization, null, [], [], error);
}
