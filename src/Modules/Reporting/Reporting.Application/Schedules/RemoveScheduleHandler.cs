using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Schedules;

public sealed class RemoveScheduleHandler(
    IScheduleRepository schedules,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<RemoveScheduleCommand, bool>
{
    public async Task<Result<bool>> Handle(RemoveScheduleCommand request, CancellationToken ct)
    {
        var schedule = await schedules.GetByIdAsync(request.TenantId, request.Id, ct);

        // Quitar algo que ya no está no es un error: es el estado que se pedía.
        if (schedule is null)
            return Result<bool>.Success(true);

        schedules.Remove(schedule);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
