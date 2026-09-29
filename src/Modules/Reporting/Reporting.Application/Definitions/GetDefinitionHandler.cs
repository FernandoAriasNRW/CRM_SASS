using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

public sealed class GetDefinitionHandler(IReportRepository reports)
    : IQueryHandler<GetDefinitionQuery, ReportDefinition>
{
    public async Task<Result<ReportDefinition>> Handle(GetDefinitionQuery request, CancellationToken ct)
    {
        var report = await reports.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (report is null)
            return Result<ReportDefinition>.Failure("El informe no existe");

        var definition = report.ReadDefinition();

        return definition is null
            ? Result<ReportDefinition>.Failure("Este informe no es a medida: no tiene definición")
            : Result<ReportDefinition>.Success(definition);
    }
}
