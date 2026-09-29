using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;

namespace Reporting.Application.Tags;

public sealed class SetReportTagsHandler(
    IReportRepository reports,
    IReportingUnitOfWork unitOfWork,
    ITagCatalog tagCatalog) : ICommandHandler<SetReportTagsCommand, bool>
{
    public const string ReportNotFound = "El informe no existe";

    public async Task<Result<bool>> Handle(SetReportTagsCommand request, CancellationToken ct)
    {
        var report = await reports.GetByIdAsync(request.TenantId, request.ReportId, ct);
        if (report is null)
            return Result<bool>.Failure(ReportNotFound);

        var tagIds = TagIdList.Normalize(request.TagIds);
        var error = await TagIdList.ValidateAsync(tagCatalog, request.TenantId, tagIds, ct);
        if (error is not null)
            return Result<bool>.Failure(error);

        report.SetTags(tagIds);
        await reports.UpdateAsync(report, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
