using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Definitions;

namespace Reporting.Application.Definitions;

public sealed class SaveDefinitionHandler(
    IReportRepository reports,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<SaveDefinitionCommand, bool>
{
    public async Task<Result<bool>> Handle(SaveDefinitionCommand request, CancellationToken ct)
    {
        var report = await reports.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (report is null)
            return Result<bool>.Failure("El informe no existe");

        var result = report.Define(request.Definition);
        if (result.IsFailure)
            return Result<bool>.Failure(result.Error!);

        await reports.UpdateAsync(report, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
