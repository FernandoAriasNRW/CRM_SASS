using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Exports;

public sealed class GetExportHandler(IExportRepository exports)
    : IQueryHandler<GetExportQuery, ExportDto>
{
    public async Task<Result<ExportDto>> Handle(GetExportQuery request, CancellationToken ct)
    {
        var export = await exports.GetByIdAsync(request.TenantId, request.ExportId, ct);

        return export is null
            ? Result<ExportDto>.Failure("Esa exportación no existe")
            : Result<ExportDto>.Success(RequestExportHandler.ToDto(export));
    }
}
