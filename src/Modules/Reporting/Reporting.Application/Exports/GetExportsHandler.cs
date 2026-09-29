using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Exports;

public sealed class GetExportsHandler(IExportRepository exports)
    : IQueryHandler<GetExportsQuery, IReadOnlyList<ExportDto>>
{
    public async Task<Result<IReadOnlyList<ExportDto>>> Handle(GetExportsQuery request, CancellationToken ct)
    {
        var list = await exports.ForReportAsync(request.TenantId, request.ReportId, ct);

        return Result<IReadOnlyList<ExportDto>>.Success(
            list.Select(RequestExportHandler.ToDto).ToList());
    }
}
