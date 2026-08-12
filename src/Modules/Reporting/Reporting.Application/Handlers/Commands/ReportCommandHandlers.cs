using BuildingBlocks.Application.Abstractions;
using Reporting.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Application.Commands;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Handlers.Commands;

public sealed class CreateReportHandler(
    IReportRepository repository,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<CreateReportCommand, Report>
{
    public async Task<Result<Report>> Handle(CreateReportCommand request, CancellationToken ct)
    {
        var type = ReportType.FromName<ReportType>(request.Type);
        var format = ReportFormat.FromName<ReportFormat>(request.Format);

        if (type is null || format is null)
            return Result<Report>.Failure("Invalid report type or format");

        var reportResult = Report.Create(request.TenantId, request.CreatedById, request.Name, type, format, request.Parameters);
        if (reportResult.IsFailure)
            return Result<Report>.Failure(reportResult.Error!);

        await repository.AddAsync(reportResult.Value!, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<Report>.Success(reportResult.Value!);
    }
}

public sealed class GenerateReportHandler(
    IReportRepository repository,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<GenerateReportCommand, bool>
{
    public async Task<Result<bool>> Handle(GenerateReportCommand request, CancellationToken ct)
    {
        var report = await repository.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (report is null)
            return Result<bool>.Failure("Report not found");

        try
        {
            report.MarkAsGenerated($"/reports/{report.Id}/{report.Name}.{request.Format.ToLower()}");
            await repository.UpdateAsync(report, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            report.MarkAsFailed(ex.Message);
            await repository.UpdateAsync(report, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Result<bool>.Failure(ex.Message);
        }
    }
}
