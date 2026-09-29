using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Schedules;

public sealed class GetSchedulesHandler(IScheduleRepository schedules)
    : IQueryHandler<GetSchedulesQuery, IReadOnlyList<ScheduleDto>>
{
    public async Task<Result<IReadOnlyList<ScheduleDto>>> Handle(GetSchedulesQuery request, CancellationToken ct)
    {
        var list = await schedules.ForReportAsync(request.TenantId, request.ReportId, ct);

        return Result<IReadOnlyList<ScheduleDto>>.Success(
            list.Select(ScheduleReportHandler.ToDto).ToList());
    }
}
