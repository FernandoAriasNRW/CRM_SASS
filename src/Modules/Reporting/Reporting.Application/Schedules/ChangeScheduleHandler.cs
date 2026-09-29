using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Domain;
using Reporting.Application.Abstractions;
using Reporting.Application.Abstractions.Repositories;
using Reporting.Domain.Entities;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Schedules;

public sealed class ChangeScheduleHandler(
    IScheduleRepository schedules,
    IReportingUnitOfWork unitOfWork) : ICommandHandler<ChangeScheduleCommand, bool>
{
    public async Task<Result<bool>> Handle(ChangeScheduleCommand request, CancellationToken ct)
    {
        var schedule = await schedules.GetByIdAsync(request.TenantId, request.Id, ct);
        if (schedule is null)
            return Result<bool>.Failure("Esa programación no existe");

        if (request.IsActive) schedule.Activate();
        else schedule.Deactivate();

        await unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
