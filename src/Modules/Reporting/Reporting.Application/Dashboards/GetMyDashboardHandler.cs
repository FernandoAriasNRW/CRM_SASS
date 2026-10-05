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

public sealed class GetMyDashboardHandler(
    TimeProvider timeProvider,
    ICustomDashboardRepository dashboards,
    IReportRepository reports,
    IReportingUnitOfWork unitOfWork) : IQueryHandler<GetMyDashboardQuery, MyDashboardDto>
{
    public async Task<Result<MyDashboardDto>> Handle(GetMyDashboardQuery request, CancellationToken ct)
    {
        var userDashboards = await dashboards.GetDashboardsAsync(request.TenantId, request.UserId, ct);

        var mine = userDashboards.FirstOrDefault(p => p.IsDefault && p.CreatedById == request.UserId);

        if (mine is not null)
            return Result<MyDashboardDto>.Success(ToDto(mine, request.UserId));

        // Primera vez: se crea el panel con los informes de partida. Cada uno es un informe de
        // verdad, guardado y editable, no un recuadro fijo: quien no lo quiera lo cambia o lo
        // quita, en vez de mirar algo que no puede tocar.
        var dashboard = Dashboard.CreatePersonal(request.TenantId, request.UserId);

        var created = new List<(Guid ReportId, StarterDashboard.SuggestedWidget Suggested)>();

        foreach (var suggested in StarterDashboard.Reports())
        {
            var report = Report.Create(
                timeProvider.GetUtcNow().UtcDateTime,
                request.TenantId, request.UserId, suggested.Name,
                ReportType.Custom, ReportFormat.Csv);

            if (report.IsFailure) continue;

            var defined = report.Value!.Define(suggested.Definition);

            // Un informe de partida que no valide es un fallo nuestro, no del usuario. Se salta
            // en vez de tumbar la creación del panel: es preferible un panel con cinco recuadros
            // que una pantalla que no abre. La prueba que recorre esta lista lo vigila.
            if (defined.IsFailure) continue;

            await reports.AddAsync(report.Value!, ct);
            created.Add((report.Value!.Id, suggested));
        }

        var placement = dashboard.Place(new DashboardLayout(StarterDashboard.Place(created)));
        if (placement.IsFailure)
            return Result<MyDashboardDto>.Failure(placement.Error!);

        await dashboards.AddAsync(dashboard, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<MyDashboardDto>.Success(ToDto(dashboard, request.UserId));
    }

    internal static MyDashboardDto ToDto(Dashboard panel, Guid userId) => new(
        panel.Id, panel.Title, panel.CreatedById == userId, panel.IsPublic,
        panel.ReadLayout().Placed);
}
